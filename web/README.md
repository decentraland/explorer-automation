# Web Automation Tests

[Playwright](https://playwright.dev/) UI automation for the Decentraland **web dapp** at `https://decentraland.org` (and the cross-platform handoff into the desktop client). For the desktop test stack see [../explorer/README.md](../explorer/README.md). For repo-wide context see [../README.md](../README.md).

## Specs

| Tag | Spec | Tests | Auth method | Notes |
|---|---|---|---|---|
| `@web` | [tests/auth-new-user.spec.ts](tests/auth-new-user.spec.ts) | new user (no newsletter) / new user (with newsletter) / new user with avatar customization | **mocked web3 wallet** (`generatePrivateKey()` per test) | no OTP cost, runs unlimited |
| `@web` | [tests/auth-recurrent-user.spec.ts](tests/auth-recurrent-user.spec.ts) | recurrent via web3 / recurrent via email + OTP | both | the OTP test is the only one in the suite that consumes a Thirdweb code |
| `@web` | [tests/download.spec.ts](tests/download.spec.ts) | clicking "DOWNLOAD FOR <platform>" hero CTA fires a `download` event | none | no auth needed |
| `@cross` | [tests/web-to-inworld-handoff.spec.ts](tests/web-to-inworld-handoff.spec.ts) | web login → bridge file → desktop client lands in-world | OTP | **currently `test.describe.skip`** until the dapp's "Jump Into Decentraland" CTA is captured via codegen |

### Web3 wallet tests — how they work

Wallet specs use Synpress' **mocked-wallet** approach (`@synthetixio/synpress` + `@synthetixio/ethereum-wallet-mock`). The mock injects a fake `window.ethereum` provider into the page; `personal_sign` requests are intercepted and signed on the Node side by `viem` using a known private key. **No MetaMask extension is loaded — the tests run headless on standard `@playwright/test`.**

The pattern is wrapped in [helpers/wallet.ts](helpers/wallet.ts) (`setupMockedWallet`) and exposed to specs via [fixtures/wallet-fixture.ts](fixtures/wallet-fixture.ts) (`walletTest`). Specs that need the wallet import `walletTest`; OTP / non-wallet specs continue to import the plain `@playwright/test`.

For new-user tests the helper is paired with `mockNoProfileOnCatalysts(page)` and an explicit `redirectTo` query param — both required to defeat the dapp's catalyst-profile existence check and the `useSkipSetup` feature-flag shortcut, otherwise the dapp routes fresh wallets to the homepage instead of `/auth/quick-setup`. The recurrent web3 test self-bootstraps: registers a fresh wallet via the new-user flow, drops the catalyst mock, re-logs in with the same key.

## Prerequisites

- **Node.js 20+**
- **Chromium** for Playwright: `npx playwright install chromium`
- A `.env` at the **repo root** populated from [../.env.example](../.env.example) — IMAP credentials for the OTP recurrent test (`EXPLORER_IMAP_*`). Web3 tests need no extra env vars; the recurrent web3 test self-bootstraps a fresh wallet.
- For the OTP recurrent test, `EXPLORER_IMAP_USER` (no `+alias`) must already be a registered Decentraland account.

For `@cross` tests you additionally need:

- [AltTester Desktop](https://alttester.com/alttester/) running and licensed
- The .NET 10 SDK (the spec shells out to `dotnet test` for the in-world check)
- [MetaForge CLI](https://github.com/decentraland/metaforge) on `PATH`
- A test identity provisioned: `../scripts/setup-test-identity.sh`

## Install

```bash
# from repo root
cd web
npm install
npx playwright install chromium
```

## Running

```bash
# from web/
npm test               # all @web tests (3 new-user web3 + 2 recurrent + download)
npm run test:headed    # same, with a visible browser
npm run test:ui        # Playwright UI mode (interactive debug + time-travel)

npm run typecheck      # tsc --noEmit
npm run report         # open the last HTML report
```

For ad-hoc filtering, use Playwright directly:

```bash
npx playwright test -g "recurrent"
npx playwright test -g "avatar"
npx playwright test tests/download.spec.ts
```

### OTP rate limits

Only the recurrent-user OTP test consumes a Thirdweb code (~3/min limit per recipient). All new-user variants are web3-mocked and have no rate limit. For CI / heavy loops, populate `EXPLORER_ALTERNATE_EMAILS` in `.env` with addresses that route to the same inbox if you need a fallback for the OTP test.

### Watching OTPs during local debugging

For OTP-flavored debugging or `playwright codegen` recording, [`scripts/watch-otp.mjs`](scripts/watch-otp.mjs) tails the inbox and prints new OTPs in real time so you don't have to context-switch to Gmail:

```bash
ALIAS="decentralande2e+rec$(openssl rand -hex 3)@gmail.com"
echo "use: $ALIAS"
node scripts/watch-otp.mjs "$ALIAS"
```

## How the cross handoff works (when wired up)

```
┌──────────────┐      OTP via IMAP       ┌──────────────────┐
│   Playwright │  ─────────────────────► │  decentraland.org│
│   (this dir) │                         │       /auth      │
└──────┬───────┘                         └────────┬─────────┘
       │                                          │  "Jump In"
       │                                          ▼
       │                         ┌──────────────────────────┐
       │                         │ auth-token-bridge.txt    │
       │                         │ (Application Support/    │
       │                         │  DecentralandLauncher…)  │
       │                         └────────┬─────────────────┘
       │                                  │  on launch
       │                                  ▼
       │ shells out to            ┌──────────────────┐
       └──── dotnet test ──────►  │  Explorer (Unity)│
            (CrossVerify fixture) │  + AltTester     │
                                  └──────────────────┘
```

The web side never talks to AltTester directly (no JS/TS SDK exists). The C# fixture [`CrossPlatformVerificationTests.cs`](../explorer/Tests/Tests/CrossPlatformVerificationTests.cs) connects via AltTester and asserts the player reached in-world.

## Project structure

```
web/
├── package.json
├── tsconfig.json
├── playwright.config.ts
├── helpers/
│   ├── env.ts             # loads ../.env, requireEnv()/optionalEnv()
│   ├── otp-mailbox.ts     # IMAP poll for the OTP code (mailparser-based body extraction)
│   ├── wallet.ts          # mocked-wallet setup (viem signing + ethereum-wallet-mock)
│   ├── token-bridge.ts    # platform-aware path + wait/read/remove
│   └── explorer-runner.ts # spawn Explorer, shell out to dotnet test
├── fixtures/
│   └── wallet-fixture.ts  # walletTest = ethereumWalletMockFixtures
├── pages/
│   ├── LandingPage.ts     # https://decentraland.org → Sign In
│   ├── AuthPage.ts        # /auth → email submit + OTP entry + clickMetaMaskButton
│   ├── QuickSetupPage.ts  # /auth/quick-setup → username / newsletter / ToS / avatar / LET'S GO
│   └── HomePage.ts        # post-login homepage + DOWNLOAD CTA
├── scripts/
│   └── watch-otp.mjs      # tail the inbox during codegen / debugging
└── tests/
    ├── auth-new-user.spec.ts           # @web — 3 web3 tests (no newsletter / newsletter / avatar)
    ├── auth-recurrent-user.spec.ts     # @web — recurrent via web3 + recurrent via OTP
    ├── download.spec.ts                # @web — launcher download
    └── web-to-inworld-handoff.spec.ts  # @cross — skipped
```

## Conventions

See [CLAUDE.md](CLAUDE.md) for the full conventions (POM rules, tagging, selector preferences, strict TS settings).

## Adding tests / page objects

- New page → add a class under `pages/` exposing typed methods that hide locator details.
- New spec → place under `tests/`, name it `*.spec.ts`, tag the `describe` with `@web` or `@cross`.
- New cross-cutting utility → place under `helpers/`, never in a spec.
- For new flows, the fastest path is `npx playwright codegen <url>` + `node scripts/watch-otp.mjs <email>` in two terminals. Paste the recorded snippet, fold it into the matching POM.
