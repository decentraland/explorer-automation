# Web Automation Tests

[Playwright](https://playwright.dev/) UI automation for the Decentraland **web dapp** at `https://decentraland.org` (and the cross-platform handoff into the desktop client). For the desktop test stack see [../explorer/README.md](../explorer/README.md). For repo-wide context see [../README.md](../README.md).

## Specs

| Tag | Spec | Tests | Auth method | Notes |
|---|---|---|---|---|
| `@web` | [tests/auth-new-user.spec.ts](tests/auth-new-user.spec.ts) | new user (no newsletter) / with newsletter / with avatar customization | mocked web3 wallet | no OTP cost, runs unlimited |
| `@web` | [tests/auth-otp-new-user.spec.ts](tests/auth-otp-new-user.spec.ts) | new user via email + OTP | OTP | consumes 1 OTP/run |
| `@web` | [tests/auth-recurrent-user.spec.ts](tests/auth-recurrent-user.spec.ts) | recurrent via web3 (self-bootstrap) / recurrent via email + OTP | both | the OTP test consumes 1 Thirdweb code |
| `@web` | [tests/auth-cross-sites.spec.ts](tests/auth-cross-sites.spec.ts) | session persists across `/marketplace`, `/builder`, `/account` after web3 signup | mocked web3 wallet | regression guard for cross-route session |
| `@web` | [tests/auth-web3-redirect.spec.ts](tests/auth-web3-redirect.spec.ts) | recurrent web3 login with `redirectTo=/marketplace` lands on `/marketplace` | mocked web3 wallet | verifies dapp respects the `redirectTo` query param (used by launcher + sister dapps) |
| `@web` | [tests/auth-request-page.spec.ts](tests/auth-request-page.spec.ts) | `dcl_personal_sign` + `eth_sendTransaction` via the **RequestPage** flow (`/auth/requests/<id>`) | mocked web3 wallet | exercises the desktop-handoff signature broker via `auth-api.decentraland.org` |
| `@web` | [tests/auth-switch-method.spec.ts](tests/auth-switch-method.spec.ts) | sign up via OTP, then via web3 wallet on a fresh page in the same context | both | catches cross-method state pollution; 1 OTP/run |
| `@web` | [tests/download.spec.ts](tests/download.spec.ts) | clicking "DOWNLOAD FOR <platform>" hero CTA fires a `download` event | none | no auth needed |
| `@webgpu` | [tests/auth-web3-avatar-setup.spec.ts](tests/auth-web3-avatar-setup.spec.ts) | new user web3 + Unity-rendered avatar editor (full customization / skip), ends on `/download` | mocked web3 wallet | requires the `webgpu` Playwright project (1200x997 + SwiftShader); excluded from `npm test` |
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
npm test               # all @web tests (no GPU); the per-PR / CI suite
npm run test:headed    # same, with a visible browser
npm run test:ui        # Playwright UI mode (interactive debug + time-travel)
npm run test:webgpu    # the @webgpu avatar-editor specs (headed Chrome + GPU)
npm run test:all       # everything: @web + @webgpu (+ @cross, currently skipped)

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
ALIAS="your-account+rec$(openssl rand -hex 3)@example.com"   # use the address from .env
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
│   ├── auth-server.ts     # auth-api.decentraland.org client (createRequest + pollOutcome)
│   ├── identity.ts        # ephemeral message + auth chain (RequestPage flow)
│   ├── token-bridge.ts    # platform-aware path + wait/read/remove
│   └── explorer-runner.ts # spawn Explorer, shell out to dotnet test
├── fixtures/
│   └── wallet-fixture.ts  # walletTest = ethereumWalletMockFixtures
├── pages/
│   ├── LandingPage.ts     # https://decentraland.org → Sign In
│   ├── AuthPage.ts        # /auth → email submit + OTP entry + clickMetaMaskButton
│   ├── QuickSetupPage.ts  # /auth/quick-setup → username / newsletter / ToS / avatar / LET'S GO
│   ├── AvatarSetupPage.ts # /avatar-setup → Unity 3D avatar editor (relative-coord clicks)
│   └── HomePage.ts        # post-login homepage + DOWNLOAD CTA
├── scripts/
│   └── watch-otp.mjs      # tail the inbox during codegen / debugging
└── tests/
    ├── auth-new-user.spec.ts           # @web — 3 web3 tests (no newsletter / newsletter / avatar)
    ├── auth-otp-new-user.spec.ts       # @web — OTP new-user signup
    ├── auth-recurrent-user.spec.ts     # @web — recurrent via web3 + recurrent via OTP
    ├── auth-cross-sites.spec.ts        # @web — session across marketplace/builder/account
    ├── auth-web3-redirect.spec.ts      # @web — redirectTo=/marketplace lands on /marketplace
    ├── auth-request-page.spec.ts       # @web — RequestPage dcl_personal_sign + eth_sendTransaction
    ├── auth-switch-method.spec.ts      # @web — OTP signup, then web3 signup in same context
    ├── auth-web3-avatar-setup.spec.ts  # @webgpu — Unity 3D avatar editor (full / skip)
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
