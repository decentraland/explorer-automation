# CLAUDE.md

Guidance for Claude Code working in the **TypeScript / Playwright** stack under `web/`.

## Scope

This stack covers two test classes:

- **`@web` tests** — pure browser flows starting at `https://decentraland.org` and navigating into `https://decentraland.org/auth`. Initial scope is **email + OTP login only**. No desktop client involvement.
- **`@cross` tests** — end-to-end web → desktop. Same login flow, then click "Jump Into Decentraland" in the dapp, wait for `auth-token-bridge.txt` to appear, launch the instrumented Explorer, and verify it reaches in-world via the C# fixture in `../explorer/Tests/`.

## Layout

```
web/
├── package.json
├── tsconfig.json
├── playwright.config.ts
├── helpers/
│   ├── env.ts             # loads ../.env, requireEnv()/optionalEnv()
│   ├── otp-mailbox.ts     # IMAP poller — mirrors explorer/Tests/Common/OtpMailbox.cs
│   ├── token-bridge.ts    # auth-token-bridge.txt path, wait/read/remove
│   └── explorer-runner.ts # spawn metaforge + verifyExplorerInWorld via dotnet test
├── pages/                 # Page Object Model
│   ├── LandingPage.ts
│   └── AuthPage.ts
└── tests/
    ├── auth-login.spec.ts          # @web
    └── web-to-inworld-handoff.spec.ts  # @cross
```

## Conventions

- **POM only** — tests must not contain raw selectors. New UI surface needs a page object first.
- **`@web` vs `@cross`** — every `test.describe` opens with the appropriate tag. Playwright's `projects` config uses `grep` to route tests; mistagging means the test won't run.
- **Strict TS** — `tsconfig.json` has `strict`, `noImplicitAny`, and `noUncheckedIndexedAccess`. Don't use `any`. Don't suppress with `// @ts-ignore`.
- **`await` everything** — every Playwright API and helper returns a Promise. Unawaited Promises silently corrupt test ordering.
- **No new helpers outside `helpers/`** — keep cross-cutting utilities (env, IMAP, file I/O, child processes) there. Tests only import from `helpers/`, `pages/`, and `@playwright/test`.
- **Selector preference** — `getByRole` → `getByText` → `getByTestId` → CSS as last resort. Never select on class names.
- **Logging** — use `console.log` sparingly for things that aid post-failure debugging (e.g., the OTP poll). Playwright's screenshot/trace/video on failure carry the rest.
- **Imports use `.js` extensions** — required by NodeNext module resolution even though source files are `.ts`. Example: `import { ... } from '../helpers/env.js';`.

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

npm run test:web     # @web only
npm run test:cross   # @cross only (needs identity, AltTester Desktop running)
npm test             # both
```

Cross tests need:

- AltTester Desktop running on `127.0.0.1:13000`.
- `.env` at the repo root with IMAP credentials.
- `metaforge` on PATH.

## Don't

- Don't add per-test waits with hard-coded sleeps. Use Playwright's auto-waiting or `waitForURL` / `waitFor`.
- Don't share state between specs via module globals — Playwright runs files in parallel.
- Don't import from `../explorer/Tests/`. The two stacks integrate only via the bridge file and the `dotnet test` shell-out.
