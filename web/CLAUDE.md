# CLAUDE.md

Guidance for Claude Code working in the **TypeScript / Playwright** stack under `web/`.

## Scope

This stack covers two test classes:

- **`@web` tests** — pure browser flows against `https://decentraland.org`, `/auth`, `/auth/quick-setup`, `/auth/requests/<id>`, and the dapp sub-routes (`/marketplace`, `/builder`, `/account`). Covers new-user signup (web3-mocked + OTP), recurrent login (both methods), avatar customization, cross-route session, the RequestPage signature broker (`dcl_personal_sign` + `eth_sendTransaction`), method switching, and launcher download. No desktop client involvement.
- **`@cross` tests** — end-to-end web → desktop. Web login, click "Jump Into Decentraland", wait for `auth-token-bridge.txt`, launch the instrumented Explorer, verify it reaches in-world via the C# fixture in `../explorer/Tests/`. Currently `test.describe.skip`.
- **`@webgpu` tests** — Unity-rendered avatar editor at `/avatar-setup`. Run via `npm run test:webgpu` in a dedicated Playwright project with WebGPU/Vulkan/SwiftShader Chrome flags and a fixed 1200×997 viewport. Excluded from `npm test` because they're slow and need GPU emulation set up. Driven by relative-coordinate clicks on the WearablePreview iframe; coordinates are calibrated for that exact viewport.

## Layout

```
web/
├── package.json
├── tsconfig.json
├── playwright.config.ts
├── helpers/
│   ├── env.ts             # loads ../.env, requireEnv()/optionalEnv()
│   ├── otp-mailbox.ts     # IMAP poller — mirrors explorer/Tests/Common/OtpMailbox.cs
│   ├── wallet.ts          # setupMockedWallet + applyPersonalSignOverride + rebindWalletMock + mockNoProfileOnCatalysts
│   ├── auth-server.ts     # auth-api client (createRequest + pollOutcome) for RequestPage
│   ├── identity.ts        # ephemeral message + auth chain for RequestPage
│   ├── token-bridge.ts    # auth-token-bridge.txt path, wait/read/remove
│   └── explorer-runner.ts # spawn metaforge + verifyExplorerInWorld via dotnet test
├── fixtures/
│   └── wallet-fixture.ts  # walletTest = ethereumWalletMockFixtures
├── pages/                 # Page Object Model
│   ├── LandingPage.ts
│   ├── AuthPage.ts
│   ├── QuickSetupPage.ts
│   ├── AvatarSetupPage.ts
│   └── HomePage.ts
└── tests/
    ├── auth-new-user.spec.ts           # @web — 3 web3 tests (no newsletter / newsletter / avatar)
    ├── auth-otp-new-user.spec.ts       # @web — OTP new-user signup
    ├── auth-recurrent-user.spec.ts     # @web — recurrent web3 + recurrent OTP
    ├── auth-cross-sites.spec.ts        # @web — session across marketplace/builder/account
    ├── auth-web3-redirect.spec.ts      # @web — redirectTo=/marketplace lands on /marketplace
    ├── auth-request-page.spec.ts       # @web — RequestPage dcl_personal_sign + eth_sendTransaction
    ├── auth-switch-method.spec.ts      # @web — OTP signup, then web3 signup in same context
    ├── auth-web3-avatar-setup.spec.ts  # @webgpu — Unity 3D avatar editor (full / skip)
    ├── download.spec.ts                # @web — launcher download
    └── web-to-inworld-handoff.spec.ts  # @cross (skipped)
```

## Conventions

- **POM only** — tests must not contain raw selectors. New UI surface needs a page object first.
- **`@web` vs `@cross`** — every `test.describe` (or top-level test name) carries the appropriate tag. Playwright's `projects` config uses `grep` to route tests; mistagging means the test won't run.
- **Strict TS** — `tsconfig.json` has `strict`, `noImplicitAny`, `noUncheckedIndexedAccess`. Don't use `any`. Don't suppress with `// @ts-ignore`. Module augmentations live in `types/` (e.g. `ethereum-wallet-mock.d.ts` re-exports the synpress fixtures' types because the package's own barrel re-exports break under NodeNext resolution).
- **`await` everything** — every Playwright API and helper returns a Promise. Unawaited Promises silently corrupt test ordering.
- **No new helpers outside `helpers/`** — cross-cutting utilities live there. Tests only import from `helpers/`, `pages/`, `fixtures/`, and `@playwright/test`.
- **Selector preference** — `getByRole` → `getByText` → `getByTestId` → CSS as last resort. Never select on class names.
- **Logging** — use `console.log` sparingly. Playwright's screenshot/trace/video on failure carry the rest.
- **Imports use `.js` extensions** — required by NodeNext module resolution even though source files are `.ts`. Example: `import { ... } from '../helpers/env.js';`.

## Test fixtures: `walletTest` vs plain `test`

Wallet specs import `walletTest` from `fixtures/wallet-fixture.js`; OTP / non-wallet specs use plain `import { test } from '@playwright/test'`. Both can coexist in one file (see `tests/auth-recurrent-user.spec.ts`).

`walletTest` injects a mocked `window.ethereum` provider into every page it launches. Don't use it for tests that need a clean browser state — the OTP recurrent test, for example, uses plain `test` so the auth screen sees no wallet at all.

The mock is set up via `setupMockedWallet(page, ethereumWalletMock, { privateKey, redirectTo })` in `helpers/wallet.ts`. It must be called BEFORE clicking the MetaMask button. The helper is idempotent on the same page — calling twice (e.g. register-then-recurrent flow) re-navigates and re-binds the mock state, but only installs the one-time plumbing once.

For new-user tests pair `setupMockedWallet` with `mockNoProfileOnCatalysts(page)` and an explicit `redirectTo` — both are required to defeat the dapp's `useEnsureProfile` (catalyst-based existence check) and `useSkipSetup` (feature-flag-based shortcut) so the user actually reaches `/auth/quick-setup`. The recurrent web3 test self-bootstraps: registers a fresh wallet via the new-user flow, drops the catalyst mock, re-logs in with the same key.

### Re-binding the mock after navigation

`page.goto` wipes JS state, so any test that navigates after sign-in must re-establish the wallet mock on the new page. Two options:

- **`rebindWalletMock(page, mock, privateKey)`** — heavier: re-runs synpress' `connectToDapp` + `importWalletFromPrivateKey` on the current page, then reapplies the personal_sign override. Use for cross-route nav (`/marketplace`, `/builder`, `/account`).
- **`installAutoWalletMockInitScript(page, address)` + `applyPersonalSignOverride(page)`** — lighter: an init script auto-mocks Web3Mock with the right address as soon as it appears on the new page; `applyPersonalSignOverride` patches `personal_sign` post-load. Use for routes that probe wallet state mid-load (e.g. `/auth/requests/<id>`) — the heavier rebind re-introduces the mock's default address mid-handshake there and crashes signing flows.

## Cross-platform handoff contract

`@cross` tests rely on these external pieces:

1. The dapp's "Jump Into Decentraland" CTA writes `auth-token-bridge.txt`.
2. Path: `~/Library/Application Support/DecentralandLauncherLight/auth-token-bridge.txt` (macOS).
3. The Decentraland Launcher's `TokenFileAuthenticator` reads + deletes this file on startup.
4. Verification runs through `explorer/Tests/Tests/CrossPlatformVerificationTests.cs::TestExplorerIsInWorldFromTokenBridge`.

If any of those move, update `helpers/token-bridge.ts` or `helpers/explorer-runner.ts` accordingly.

## Running

```bash
# from web/
npm install
npx playwright install chromium

npm test               # all @web tests
npm run test:headed    # same, with a visible browser
npm run test:ui        # Playwright UI mode
npm run typecheck      # tsc --noEmit
```

## Don't

- Don't add per-test waits with hard-coded sleeps. Use Playwright's auto-waiting or `waitForURL` / `waitFor`.
- Don't share state between specs via module globals — Playwright runs files in parallel.
- Don't import from `../explorer/Tests/`. The two stacks integrate only via the bridge file and the `dotnet test` shell-out.
- Don't reach for `chromium.launchPersistentContext` to load a real MetaMask extension — the mock approach in `helpers/wallet.ts` covers all current wallet test cases without that complexity.
