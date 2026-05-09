# Web Automation Tests

[Playwright](https://playwright.dev/) UI automation for the Decentraland **web dapp** at `https://decentraland.org` (and the cross-platform handoff into the desktop client). For the desktop test stack see [../explorer/README.md](../explorer/README.md). For repo-wide context see [../README.md](../README.md).

## Specs

| Tag                      | Spec                                                                                               | Tests                                                                                            | Auth method                          | Notes                                                                                                    |
| ------------------------ | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ | ------------------------------------ | -------------------------------------------------------------------------------------------------------- |
| `@web`                   | [tests/auth/specs/new-user.spec.ts](tests/auth/specs/new-user.spec.ts)                             | new user (no newsletter) / with newsletter / with avatar customization                           | mocked web3 wallet                   | no OTP cost, runs unlimited                                                                              |
| `@web`                   | [tests/auth/specs/otp-new-user.spec.ts](tests/auth/specs/otp-new-user.spec.ts)                     | new user via email + OTP                                                                         | OTP                                  | consumes 1 OTP/run                                                                                       |
| `@web`                   | [tests/auth/specs/recurrent-user.spec.ts](tests/auth/specs/recurrent-user.spec.ts)                 | recurrent via web3 (self-bootstrap) / recurrent via email + OTP                                  | both                                 | the OTP test consumes 1 Thirdweb code                                                                    |
| `@web`                   | [tests/auth/specs/cross-sites.spec.ts](tests/auth/specs/cross-sites.spec.ts)                       | session persists across `/marketplace`, `/builder`, `/account` after web3 signup                 | mocked web3 wallet                   | regression guard for cross-route session                                                                 |
| `@web`                   | [tests/auth/specs/web3-redirect.spec.ts](tests/auth/specs/web3-redirect.spec.ts)                   | recurrent web3 login with `redirectTo=/marketplace` lands on `/marketplace`                      | mocked web3 wallet                   | verifies dapp respects the `redirectTo` query param (used by launcher + sister dapps)                    |
| `@web`                   | [tests/auth/specs/request-page.spec.ts](tests/auth/specs/request-page.spec.ts)                     | `dcl_personal_sign` + `eth_sendTransaction` via the **RequestPage** flow (`/auth/requests/<id>`) | mocked web3 wallet                   | exercises the desktop-handoff signature broker via `auth-api.decentraland.org`                           |
| `@web`                   | [tests/auth/specs/switch-method.spec.ts](tests/auth/specs/switch-method.spec.ts)                   | sign up via OTP, then via web3 wallet on a fresh page in the same context                        | both                                 | catches cross-method state pollution; 1 OTP/run                                                          |
| `@web`                   | [tests/auth/specs/download.spec.ts](tests/auth/specs/download.spec.ts)                             | clicking "DOWNLOAD FOR <platform>" hero CTA fires a `download` event                             | none                                 | no auth needed                                                                                           |
| `@webgpu`                | [tests/auth/specs/web3-avatar-setup.spec.ts](tests/auth/specs/web3-avatar-setup.spec.ts)           | new user web3 + Unity-rendered avatar editor (full customization / skip), ends on `/download`    | mocked web3 wallet                   | requires the `webgpu` Playwright project (1200x997 + SwiftShader); excluded from `npm test`              |
| `@cross`                 | [tests/auth/specs/web-to-inworld-handoff.spec.ts](tests/auth/specs/web-to-inworld-handoff.spec.ts) | web login → bridge file → desktop client lands in-world                                          | OTP                                  | **currently `test.describe.skip`** until the dapp's "Jump Into Decentraland" CTA is captured via codegen |
| `@marketplace`           | [tests/marketplace/specs/browse.spec.ts](tests/marketplace/specs/browse.spec.ts)                   | public browse page renders; clicking an asset card navigates to its detail page                  | none                                 | no auth, no on-chain calls                                                                               |
| `@marketplace`           | [tests/marketplace/specs/connect-wallet.spec.ts](tests/marketplace/specs/connect-wallet.spec.ts)   | pre-seeded SSO identity makes marketplace recognize the wallet as connected                      | Synpress mocked-wallet (default key) | off-chain; specs without `.env`                                                                          |
| `@marketplace`           | [tests/marketplace/specs/account.spec.ts](tests/marketplace/specs/account.spec.ts)                 | connected wallet sees their account page                                                         | Synpress mocked-wallet (default key) | off-chain                                                                                                |
| `@marketplace @on-chain` | [tests/marketplace/specs/buy-and-sell.spec.ts](tests/marketplace/specs/buy-and-sell.spec.ts)       | primary-buy + list + secondary-buy round-trip via meta-tx relayer on Polygon Amoy                | real EOAs (`WALLET_A/B_PRIVATE_KEY`) | runs serial under `--workers=1`; requires funded wallets + ERC20 approval                                |

### Web3 wallet tests — how they work

Wallet specs use Synpress' **mocked-wallet** approach (`@synthetixio/synpress` + `@synthetixio/ethereum-wallet-mock`). The mock injects a fake `window.ethereum` provider into the page; `personal_sign` requests are intercepted and signed on the Node side by `viem` using a known private key. **No MetaMask extension is loaded — the tests run headless on standard `@playwright/test`.**

The pattern is wrapped in [tests/auth/helpers/wallet.ts](tests/auth/helpers/wallet.ts) (`setupMockedWallet`) and exposed to specs via [shared/fixtures/wallet-fixture.ts](shared/fixtures/wallet-fixture.ts) (`walletTest`). Specs that need the wallet import `walletTest`; OTP / non-wallet specs continue to import the plain `@playwright/test`.

For new-user tests the helper is paired with `mockNoProfileOnCatalysts(page)` and an explicit `redirectTo` query param — both required to defeat the dapp's catalyst-profile existence check and the `useSkipSetup` feature-flag shortcut, otherwise the dapp routes fresh wallets to the homepage instead of `/auth/quick-setup`. The recurrent web3 test self-bootstraps: registers a fresh wallet via the new-user flow, drops the catalyst mock, re-logs in with the same key.

## Prerequisites

- **Node.js 20+**
- **Chromium** for Playwright: `npx playwright install chromium`
- A `.env` at the **repo root** populated from [../.env.example](../.env.example) — IMAP credentials for the OTP recurrent test (`IMAP_*`). Web3 tests need no extra env vars; the recurrent web3 test self-bootstraps a fresh wallet.
- For the OTP recurrent test, `IMAP_USER` (no `+alias`) must already be a registered Decentraland account.

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

Only the recurrent-user OTP test consumes a Thirdweb code (~3/min limit per recipient). All new-user variants are web3-mocked and have no rate limit. The OTP new-user spec generates a fresh `qa-<hash>@e2e.decentraland.org` per run via `generateFreshEmail()` — each address is its own rate-limit bucket. Override the domain via `EMAIL_DOMAIN` if you've pointed the suite at a different inbox.

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
├── playwright.config.ts          # 5 projects: web, cross, webgpu, marketplace, marketplace-onchain
├── .eslintrc.cjs                 # @typescript-eslint + playwright + prettier
├── .prettierrc                   # no-semi, single-quote, 120 cols
├── shared/                       # dapp-agnostic infrastructure (auth + marketplace)
│   ├── helpers/                  # env, identity, auth-identity, broadcast-wallet,
│   │                             #   wallet-setup, profile, url, synpress
│   ├── fixtures/wallet-fixture.ts  # walletTest = ethereumWalletMockFixtures
│   └── types/ethereum-wallet-mock.d.ts
└── tests/
    ├── auth/                     # browser-driven auth + cross-platform handoff
    │   ├── helpers/              # wallet (mocked Synpress), otp-mailbox, auth-server,
    │   │                         #   token-bridge, explorer-runner
    │   ├── pages/                # LandingPage, AuthPage, QuickSetupPage, AvatarSetupPage, HomePage
    │   └── specs/                # 10 specs (8 @web + 1 @webgpu + 1 @cross)
    └── marketplace/              # marketplace dapp (data-testid-first locators)
        ├── helpers/              # primary-buy, listing, accept-listing, wallet-pool
        ├── pages/                # 8 POMs: BrowsePage, AssetPage, BuyWithCryptoModal, …
        ├── fixtures/wallet-fixture.ts  # marketplaceTest + walletTest (POMs + Synpress)
        └── specs/                # 4 specs (3 @marketplace + 1 @marketplace @on-chain)
```

## Conventions

See [CLAUDE.md](CLAUDE.md) for the full conventions (layout rules, surface-aware locator policy, wallet-stack invariants, tagging, strict TS settings).

## Adding tests / page objects

- New auth page → add under `tests/auth/pages/`. New marketplace page → add under `tests/marketplace/pages/` (data-testid-first locators).
- New spec → `tests/<dapp>/specs/*.spec.ts`, tag the `describe` with `@web` / `@webgpu` / `@cross` (auth) or `@marketplace` (+ `@on-chain` if it broadcasts a tx).
- New dapp-agnostic utility → `shared/helpers/`. New surface-specific utility → `tests/<dapp>/helpers/`.
- For new flows, the fastest path is `npx playwright codegen <url>` + `node scripts/watch-otp.mjs <email>` in two terminals. Paste the recorded snippet, fold it into the matching POM.
