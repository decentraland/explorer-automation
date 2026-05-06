# CLAUDE.md

Guidance for Claude Code working in the **TypeScript / Playwright** stack under `web/`.

## Scope

This stack covers two test classes:

- **`@web` tests** — pure browser flows against `https://decentraland.org`, `/auth`, `/auth/quick-setup`. Covers new-user signup (web3-mocked, three variants), recurrent-user login (both web3 and OTP), and the launcher download CTA. No desktop client involvement.
- **`@cross` tests** — end-to-end web → desktop. Web login, click "Jump Into Decentraland", wait for `auth-token-bridge.txt`, launch the instrumented Explorer, verify it reaches in-world via the C# fixture in `../explorer/Tests/`. Currently `test.describe.skip`.

## Layout

```
web/
├── package.json
├── tsconfig.json
├── playwright.config.ts
├── helpers/
│   ├── env.ts             # loads ../.env, requireEnv()/optionalEnv()
│   ├── otp-mailbox.ts     # IMAP poller — mirrors explorer/Tests/Common/OtpMailbox.cs
│   ├── wallet.ts          # setupMockedWallet — mocked window.ethereum + viem signing
│   ├── token-bridge.ts    # auth-token-bridge.txt path, wait/read/remove
│   └── explorer-runner.ts # spawn metaforge + verifyExplorerInWorld via dotnet test
├── fixtures/
│   └── wallet-fixture.ts  # walletTest = ethereumWalletMockFixtures
├── pages/                 # Page Object Model
│   ├── LandingPage.ts
│   ├── AuthPage.ts
│   ├── QuickSetupPage.ts
│   └── HomePage.ts
└── tests/
    ├── auth-new-user.spec.ts           # @web — 3 web3 tests
    ├── auth-recurrent-user.spec.ts     # @web — web3 + OTP
    ├── download.spec.ts                # @web
    └── web-to-inworld-handoff.spec.ts  # @cross (skipped)
```

## Conventions

- **POM only** — tests must not contain raw selectors. New UI surface needs a page object first.
- **`@web` vs `@cross`** — every `test.describe` (or top-level test name) carries the appropriate tag. Playwright's `projects` config uses `grep` to route tests; mistagging means the test won't run. Auth specs additionally carry `@auth` and the download spec carries `@download`; these finer-grained tags drive the suite selector in `.github/workflows/web-e2e.yml`.
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
