# CLAUDE.md

Guidance for Claude Code working in the **TypeScript / Playwright** stack under `web/`. This stack covers two surface areas:

- **Auth flows** at `https://decentraland.org/auth/*` and the post-login dapp routes — driven by browser interactions through `/auth/login`. The product under test is the auth UI itself.
- **Marketplace dapp** at `https://decentraland.org/marketplace/` — driven via pre-seeded SSO identity in `localStorage` so the dapp boots already-signed-in. The product under test is the marketplace, not auth.

The two surfaces share infrastructure (`shared/helpers/`, `shared/fixtures/`, `shared/types/`) but use **different authentication strategies**. Don't unify them — each is correct for its goal.

## Skills to invoke

When working in this stack, these skills auto-activate based on the task. They live under the repo's `.claude/skills/`:

- **`dcl-testing-playwright`** — Decentraland-specific Playwright invariants: wallet stack ordering (`injectAuthIdentity` + `setupBroadcastWallet`), Web3Mock + Synpress integration, meta-tx flow, locator priority for marketplace, on-chain verification via viem. Invoke for any change to `shared/`, marketplace specs/POMs/helpers, or any wallet/auth flow.
- **`playwright-best-practices`** — General Playwright technique: locators, fixtures, waiting, debugging, CI/CD, project tagging. Invoke for any new spec or POM. Where this repo deviates (notably locator priority for marketplace), `dcl-testing-playwright` documents the rationale and wins on conflicts.
- **`dcl-testing`** — Paradigm-agnostic test standards (semantic naming, behaviour-not-implementation, test independence). Apply intent only — its Jest-shaped syntax doesn't translate literally to Playwright.

## Layout

```
web/
├── package.json
├── tsconfig.json
├── playwright.config.ts            # 5 projects: web, cross, webgpu, marketplace, marketplace-onchain
├── .eslintrc.cjs                   # @typescript-eslint + playwright + prettier
├── .prettierrc                     # no-semi, single-quote, 120 cols
├── shared/                         # dapp-agnostic infrastructure
│   ├── helpers/
│   │   ├── env.ts                  # requireEnv / optionalEnv (.env at repo root)
│   │   ├── identity.ts             # auth-chain primitives (RequestPage)
│   │   ├── auth-identity.ts        # injectAuthIdentity (SSO + decentraland-connect localStorage seed) + installInjectedWalletMock (Web3Mock account override + window.ethereum.request patch)
│   │   ├── broadcast-wallet.ts     # viem-driven tx broadcast (eth_sendTransaction + signTypedData)
│   │   ├── ethereum.ts             # waitForAmoyReceipt — single entry point for Amoy receipt + status assertion (used by buy-and-sell spec + accept-listing helper)
│   │   ├── profile.ts              # mockExistingProfile (catalyst route intercept)
│   │   └── url.ts                  # withEnv() — appends ?env=dev for testnet switching
│   ├── fixtures/
│   │   └── wallet-fixture.ts       # Synpress ethereumWalletMockFixtures re-export
│   └── types/
│       └── ethereum-wallet-mock.d.ts   # explicit declaration of EthereumWalletMock interface
└── tests/
    ├── landing/                    # public site (`decentraland.org/`) — login entry + hero CTAs
    │   ├── pages/
    │   │   └── LandingPage.ts      # goto / clickSignIn / downloadLauncher (pre-login) + waitForUrl (post-login URL assertion)
    │   └── specs/                  # @web specs that don't drive an auth flow
    │       └── download.spec.ts                  # @web @landing — launcher .dmg download
    ├── auth/                       # browser-driven auth + cross-platform handoff
    │   ├── helpers/
    │   │   ├── wallet.ts           # setupMockedWallet, mockNoProfileOnCatalysts, rebindWalletMock
    │   │   ├── otp-mailbox.ts      # IMAP poller for Thirdweb OTPs
    │   │   ├── auth-server.ts      # auth-api request/poll for RequestPage signing
    │   │   ├── token-bridge.ts     # auth-token-bridge.txt path/read/wait for @cross handoff
    │   │   └── explorer-runner.ts  # spawn metaforge + verify in-world via dotnet test
    │   ├── pages/
    │   │   ├── AuthPage.ts
    │   │   ├── QuickSetupPage.ts
    │   │   └── AvatarSetupPage.ts  # @webgpu Unity avatar editor
    │   └── specs/                  # all tagged @web / @cross / @webgpu — auth specs additionally carry @auth
    │       ├── new-user.spec.ts                  # @web @auth — 3 web3 signup variants
    │       ├── otp-new-user.spec.ts              # @web @auth — OTP signup
    │       ├── recurrent-user.spec.ts            # @web @auth — recurrent web3 + OTP
    │       ├── cross-sites.spec.ts               # @web @auth — session across /marketplace, /builder, /account
    │       ├── web3-redirect.spec.ts             # @web @auth — redirectTo query param handling
    │       ├── request-page.spec.ts              # @web @auth — RequestPage signature broker
    │       ├── switch-method.spec.ts             # @web @auth — switch from OTP to web3 in same context
    │       ├── web3-avatar-setup.spec.ts         # @webgpu — Unity 3D avatar editor
    │       └── web-to-inworld-handoff.spec.ts    # @cross — web → desktop (currently skipped)
    └── marketplace/                # marketplace dapp tests
        ├── helpers/
        │   ├── wallet-setup.ts         # setupTestWallet — composes injectAuthIdentity + installInjectedWalletMock + mockExistingProfile + setupBroadcastWallet
        │   ├── transactions-capture.ts # captureTransactionsPosts — observe /v1/transactions POSTs (approval + buy); spec drives auth modal directly
        │   ├── mint-decoder.ts         # decodeMintFromReceipt — extract tokenId from an ERC-721 Transfer log on a primary-buy receipt
        │   ├── nft-indexer.ts          # waitForNftIndexed — poll marketplace-api /v1/nfts until the new NFT is searchable
        │   ├── listing.ts              # captureListingResponse — listens for marketplace-api /v1/trades 201 + extracts tradeId
        │   ├── accept-listing.ts       # captureAcceptListingTxHash — listens for /v1/transactions POST + waits for Amoy receipt
        │   └── wallet-pool.ts          # 2-EOA pool, runtime role assignment by MANA balance
        ├── pages/                      # 8 POMs — testid-first locators
        │   ├── BrowsePage.ts
        │   ├── AssetPage.ts
        │   ├── BuyWithCryptoModal.ts
        │   ├── SellModal.ts
        │   ├── AuthorizationModal.ts
        │   ├── AccountPage.ts
        │   ├── Navbar.ts
        │   └── SignInPage.ts
        ├── fixtures/
        │   └── wallet-fixture.ts       # marketplaceTest (POMs only) + walletTest (POMs + Synpress + worker-scoped walletPool + test-scoped sellerWallet/buyerWallet)
        └── specs/                      # 4 specs, all tagged @marketplace (+ @on-chain where applicable)
            ├── browse.spec.ts                # @marketplace
            ├── account.spec.ts               # @marketplace
            ├── connect-wallet.spec.ts        # @marketplace
            └── buy-and-sell.spec.ts          # @marketplace @on-chain
```

## Conventions

### Imports

- **`.js` extensions are required** under NodeNext module resolution even on `.ts` source. Example: `import { walletTest } from '../../../shared/fixtures/wallet-fixture.js'`.
- Auth specs/helpers/POMs live under `tests/auth/`; sibling imports use `'../helpers/...'` / `'../pages/...'`. Cross-folder imports to `shared/` use `'../../../shared/helpers/...'` (3 levels up).
- Marketplace specs/helpers/POMs live under `tests/marketplace/` with the same depth, so the same `'../../../shared/...'` pattern applies.

### Tagging — required on every `test.describe`

Playwright's projects use `grep` to route specs. An untagged `describe` doesn't run under any project. Required tags:

- `@web` — auth-screen browser tests (default `npm test`)
- `@cross` — web → desktop handoff (currently skipped via `test.describe.skip`)
- `@webgpu` — Unity avatar editor (`npm run test:webgpu`)
- `@marketplace` — every marketplace describe block
- `@on-chain` — additionally on any spec that broadcasts a transaction. The `marketplace` project filters with `grepInvert: /@on-chain/`; the `marketplace-onchain` project filters with `grep: /@on-chain/`. **Forgetting `@on-chain` on a broadcast spec causes it to be silently skipped** by both projects.
- `@auth` / `@landing` — sub-tags on the `@web` specs that bucket them by surface (auth flows vs. landing/main-site). Drive the manual suite selector in `.github/workflows/web-e2e.yml` (`auth` / `landing` choices). Marketplace specs don't need a sub-tag — the workflow targets them by `--project=marketplace[-onchain]`.

Pure-signature flows (e.g. listing-only via `/v1/trades`, no relayer) do NOT carry `@on-chain` — they don't compete for the wallet pool.

**Substring collision (resolved)**: `npm test` now runs `playwright test --project=web`, which scopes the run to the `web` project alone. A previous version used a global `--grep @web`; because `@web` is a substring of `@webgpu`, that also pulled in the headed WebGPU specs, and because it matched the `@web`-tagged `mana-donation` / `nft-gift` describes it also dragged the 420s on-chain auth specs into the default run. Selecting the project directly avoids both (the project's own `grep: /@web\b/` + `grepInvert: /@on-chain/` do the intra-project filtering). If you ever go back to a CLI grep, use `--grep "@web\b"` — a bare `--grep @web` is global and re-introduces the collision. The `mana-donation` / `nft-gift` describes are tagged `@auth @on-chain` (not `@web`) so they only route through the `auth-onchain` project.

### Locator priority — surface-aware

The two surfaces have different stable signals; the locator priority differs accordingly:

| Surface                                       | Priority                                                                | Rationale                                                                                                                                                                                               |
| --------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Auth** (`tests/auth/pages/*`)               | `getByRole({ name })` → `getByText` → `getByTestId` → CSS (last resort) | Auth screens are semantic HTML forms; ARIA roles are stable.                                                                                                                                            |
| **Marketplace** (`tests/marketplace/pages/*`) | `data-testid` → `getByRole({ name })` → `getByText` → CSS (last resort) | Marketplace data-testids are versioned source-code constants the dapp team treats as a stable test contract. ARIA roles in `decentraland-ui`-based React components churn with every component restyle. |

The marketplace inversion is **deliberate** and contradicts `playwright-best-practices/core/locators.md`'s general guidance — see `dcl-testing-playwright`'s "Locator priority for DCL dapp UIs" section for the full rationale. Don't "fix" marketplace POMs to use `getByRole`-first; cite the testid → marketplace-source-constant mapping in a header comment on each POM class instead.

CSS selectors (`.AssetCard`, `[class*="Price"]`) require a `// TODO(testid):` comment proposing the testid the marketplace team should add. Track each in a "Pending marketplace testids" table inside this file.

### POM rules

- One class per page or modal.
- Locator methods return `Locator` (lazy — never fired).
- Action methods are `async` and return `void` or a domain value (e.g. a tx hash).
- **No assertions inside page methods** — assertions belong in specs.
- Marketplace POMs cite a testid → marketplace-source-constant mapping in a header comment.
- Always pass `{ timeout: <ms> }` explicitly to `waitFor` / `waitForURL` / `waitForResponse`. Default 30s is wrong for both fast operations (modal opens in <1s) and slow ones (Amoy receipt up to 3 min).

### Fixtures

- **Auth specs**: import `walletTest` from `../../../shared/fixtures/wallet-fixture.js` for web3 specs; `import { test, expect } from '../../../shared/fixtures/base-test.js'` for OTP/non-wallet specs. **Never import `test` from `@playwright/test` in a spec** (ESLint enforces this) — base-test's `test` is the plain one plus the CF Access route that `.zone`/`.today` runs need; without it those runs hit blank pages (see Cloudflare Access section).
- **Marketplace specs**: import from `../fixtures/wallet-fixture.js` (the marketplace-local fixture), choosing `marketplaceTest` (POMs only, no Synpress mock — for browse/public flows) or `walletTest` (POMs + Synpress — for any spec that calls `injectAuthIdentity`/`setupBroadcastWallet`).

### TypeScript

- Strict mode (`strict`, `noImplicitAny`, `noUncheckedIndexedAccess`).
- No `any`, no `// @ts-ignore`. Module augmentations live in `shared/types/`.
- `await` everything — every Playwright API and helper returns a Promise. The ESLint rule `@typescript-eslint/no-floating-promises` enforces this; don't disable.

### Lint / format

- ESLint via `npm run lint`. Notable rules: `no-floating-promises` and `no-misused-promises` (real-bug catchers), `playwright/no-skipped-test` (warn), `playwright/no-conditional-in-test` (warn).
- Prettier via `npm run format`. No semicolons, single quotes, 120 cols, no trailing commas.
- `.prettierignore` is the source of truth for what Prettier skips — `.eslintrc.cjs` `ignorePatterns` only covers ESLint. Playwright artifact dirs (`playwright-report/`, `test-results/`, `allure-results/`) plus `node_modules/`, `dist/`, and `package-lock.json` must stay listed there or `npm run format:check` will be dirty after every test run.

## Wallet stack — load-bearing invariants

These are not style preferences. Violating them produces silently-passing-but-wrong tests.

### Marketplace wallet setup ordering

In every marketplace spec that needs a connected wallet:

```ts
await injectAuthIdentity(page, privateKey)        // 1. SSO + decentraland-connect localStorage seed
await installInjectedWalletMock(page, privateKey) // 2. Web3Mock account override + window.ethereum.request patch
await mockExistingProfile(page, address)          // 3. (optional) profile mock
await setupBroadcastWallet(page, { ... })         // 4. tx broadcasting interceptors
await page.goto(...)                              // 5. THEN navigate
```

(Or use `setupTestWallet(page, privateKey)` from `tests/marketplace/helpers/wallet-setup.js` — it does steps 1–4 in order.)

For on-chain marketplace specs, prefer the `sellerWallet` / `buyerWallet` fixtures in `tests/marketplace/fixtures/wallet-fixture.ts` — they call `setupTestWallet` against the worker-scoped `walletPool` automatically and expose `{ address }`.

All four `addInitScript` calls only run on **subsequent** navigations. Calling them after `page.goto` is a silent no-op on that page.

**Why the split between `injectAuthIdentity` and `installInjectedWalletMock`**: the SSO seed is wallet-agnostic (works with WalletConnect, real MetaMask, etc.); the Web3Mock patch only applies to `@synthetixio/ethereum-wallet-mock`-backed tests. A future real-wallet test calls `injectAuthIdentity` only.

### Auth wallet setup (web3 specs)

Different — the auth tests drive the browser through `/auth/login`. Use `setupMockedWallet` from `tests/auth/helpers/wallet.js`. Call BEFORE clicking the MetaMask button. Pair with `mockNoProfileOnCatalysts(page)` and explicit `redirectTo` for new-user flows so the dapp routes through `/auth/quick-setup` instead of skipping to homepage.

### Meta-tx is the default for marketplace Polygon writes

The "BUY WITH MANA" flow on dev/zone is a **sponsored meta-transaction**, not a direct user-broadcast tx:

1. Wallet stays on Sepolia (auth chain) — never broadcasts anything.
2. Dapp asks for an EIP-712 signature with `domain.chainId = polygonAmoy.id`. viem's `account.signTypedData` signs against the domain, not the provider chain.
3. Dapp POSTs the signature to **transactions-server** at `/v1/transactions`. Response: `{ txHash: "0x..." }`.
4. Relayer broadcasts on Amoy from its own EOA — the user's address appears only inside the calldata.

Implications:

- Don't assume `eth_sendTransaction` will fire on the user's wallet — it won't.
- Don't verify by looking up the user's wallet on `amoy.polygonscan.com` — `from` is the relayer EOA. Capture `txHash` from the `/v1/transactions` POST and pass it to `waitForAmoyReceipt({ txHash })` from `shared/helpers/ethereum.js` — single entry point that constructs the viem Amoy public client, polls the receipt, and asserts `status === 'success'`. Used by `tests/marketplace/specs/buy-and-sell.spec.ts` (primary-mint test) and `helpers/accept-listing.ts`; reuse instead of inlining `createPublicClient` per call site.
- Don't treat `/status` as success — it's the in-flight polling page. Match `/\/success(\?|$|\/)/` exactly.

### Marketplace listing flow — off-chain only

The seller's "List for sale" is **not a meta-tx**:

1. Wallet signs an EIP-712 trade (`OffChainMarketplaceV2`).
2. Dapp POSTs to **marketplace-api** at `/v1/trades`. Response: `201`.
3. No relayer, no on-chain tx. The listing exists as a row in the marketplace DB until a buyer accepts.

Don't `waitForTransactionReceipt` for the listing — there's no tx. Don't confuse `/v1/trades` (marketplace-api) with `/v1/transactions` (transactions-server) — different services, different hosts.

### Wallet pool — two EOAs, roles assigned by balance

On-chain marketplace specs share a 2-EOA pool (`WALLET_A_PRIVATE_KEY` + `WALLET_B_PRIVATE_KEY`). The `marketplace-onchain` Playwright project runs under `--workers=1` (set in `npm run test:marketplace:onchain`); `fullyParallel: false` in the project config is belt-and-suspenders.

Roles (`seller`/`buyer`) are assigned at runtime by `setupWalletPool()` from `tests/marketplace/helpers/wallet-pool.js`, reading each wallet's MANA balance on Polygon Amoy. The wealthier wallet plays seller. Over many runs the assignment naturally inverts.

`setupWalletPool()` is invoked by the **worker-scoped `walletPool` fixture** in `tests/marketplace/fixtures/wallet-fixture.ts` — initialized once per worker process, reused across every test in that worker. Under `--workers=1` (the only supported mode for `marketplace-onchain`), this means one pool initialization per CI run. Tests that need a configured wallet destructure the test-scoped `sellerWallet` or `buyerWallet` fixture — those resolve `walletPool` lazily and call `setupTestWallet` against the assigned role's private key. Off-chain specs that don't destructure these fixtures don't trigger the pool setup, so they continue to run without `WALLET_A_PRIVATE_KEY` etc. in env.

`setupWalletPool()` enforces a `MIN_WALLET_MANA` precheck and throws a clear "wallet pool low" error if either wallet falls below the threshold. **Top-ups are manual** — there's no auto-fund.

### Synpress mock-account key — do NOT env-var-ize

Off-chain marketplace specs that pre-seed an SSO identity but don't call `setupBroadcastWallet` (currently `connect-wallet.spec.ts` and `account.spec.ts`) must use `SYNPRESS_DEFAULT_KEY` from `shared/helpers/synpress.ts`. That constant is the private key whose address (`0xd73b04b0e696b0945283defa3eee453814758f1a`) Synpress's `ethereumWalletMockFixtures` returns from `eth_requestAccounts`. If the seeded identity address and the wallet-mock address don't match, the dapp gets a wallet for one address but reads identity for another and never marks itself connected.

It is tempting to "clean this up" by reading from `WALLET_A_PRIVATE_KEY` instead. **Don't.** Three reasons:

1. The off-chain specs would then require `.env` to run (they currently pass with no env loaded — that's a feature, not an oversight).
2. `WALLET_A` is a real funded EOA on Polygon Amoy reserved for the on-chain wallet pool; using it on dev marketplace creates real listings/profile state on the staging backend tied to a wallet that's also doing on-chain runs.
3. The Synpress default address is a **versioned contract with `@synthetixio/ethereum-wallet-mock`**, not a secret. If Synpress ever changes its default, you want a failing test and a deliberate package bump — not a silent env-driven mismatch.

Bump the constant in `shared/helpers/synpress.ts` only as part of a `@synthetixio/ethereum-wallet-mock` version upgrade.

## Running

```bash
# from web/
npm install
npx playwright install chromium

# Auth (default)
npm test                       # @web specs (auth screens)
npm run test:headed            # same, with visible browser
npm run test:ui                # Playwright UI mode
npm run test:webgpu            # @webgpu (avatar editor; needs GPU)

# Marketplace
npm run test:marketplace            # off-chain then on-chain (sequential)
npm run test:marketplace:offchain   # parallel: browse, account, connect-wallet
npm run test:marketplace:onchain    # serial (--workers=1): buy-and-sell

# Auth on-chain (RequestPage → real Polygon Amoy broadcast; --workers=1)
npm run test:auth:onchain      # mana-donation + nft-gift

# Everything
npm run test:all               # all 6 projects

# Tooling
npm run typecheck
npm run lint
npm run format
```

When iterating on a single test, prefer `--headed --workers=1` so the browser is observable and there's no focus contention.

## Environment variables

Loaded from the repo-root `.env` (see `.env.example` for the full template). The env loader (`shared/helpers/env.ts`) calls `requireEnv` at **module-import time**, so any auth spec that imports an OTP helper hard-fails on collection if `IMAP_USER` (and friends) are missing — not at the test body. Run with a populated `../.env` or those specs won't even start. The on-chain marketplace spec uses `optionalEnv` at module level + a `haveOnChainConfig` guard, so it self-skips cleanly when wallets aren't configured; new specs with optional env should follow that pattern rather than `requireEnv` at top level.

### Cloudflare Access (required for `.zone` / `.today` targets)

- `CF_ACCESS_CLIENT_ID`, `CF_ACCESS_CLIENT_SECRET` — service-token credentials. `getCloudflareAccessHeaders()` in `shared/helpers/env.ts` returns `{ 'CF-Access-Client-Id': …, 'CF-Access-Client-Secret': … }` only when **both** env vars are set AND the resolved dapp host (`WEB_BASE_URL`, default `.org`) is `decentraland.zone` or `decentraland.today` — the CF-gated dev/staging dapps. Returns `{}` otherwise.
- **Injection mechanism — host-scoped `context.route()`, NOT `use.extraHTTPHeaders`.** `installCfAccessRoute` in `shared/fixtures/base-test.ts` installs a context-level route that adds the headers only to requests whose target host is CF-gated. Every test fixture in the repo (`base-test.ts`'s `test`, both `walletTest`s, `marketplaceTest`) layers it in via a `context` override, so specs get it for free as long as they import `test` from a repo fixture. Do NOT "simplify" this back to `use.extraHTTPHeaders`: Playwright attaches those to **every** request, and a non-safelisted header on a cross-origin CORS-mode request (module scripts / CSS from `cdn.decentraland.org`, fonts from `fonts.gstatic.com`) forces a preflight those origins reject — every dapp JS bundle fails and pages render blank. Two traps made this hard to diagnose historically: (1) while any `page.route()` is registered (Synpress Web3Mock, catalyst mocks), Playwright's interception path skips the preflight, so wallet specs passed while OTP specs and post-`unroute` phases went blank — the July 2026 zone failures; (2) sending the headers unconditionally previously also broke **`.org`** runs — same CORS mechanism, since `.org` pulls bundles from the same CDN (originally misattributed to "prod Cloudflare reacting to the headers"). Cloudflare never issues a `CF_Authorization` cookie for service-token requests, so cookie seeding is not an alternative — the headers must ride each gated-host request. The `*.api.decentraland.zone` / `.today` subdomains (auth-api, marketplace-api) don't need the headers (publicly reachable), and the host-scoped route means they don't receive them.
- **No trace/video recording — failures are diagnosed from text.** `trace` and `video` are deliberately absent from `playwright.config.ts`: they ballooned CI artifacts past 1GB per run, and traces record request headers verbatim, so gated-host runs embedded the CF service token in every `trace.zip`. On failure each test instead attaches (and echoes to stdout) a `browser-log` — console errors/warnings, uncaught page errors, failed requests with the network-layer error, HTTP >= 400 responses, main-frame navigations, and the open-page URLs at teardown — captured by `contextWithDiagnostics` in `shared/fixtures/base-test.ts`. Playwright's error-context ARIA snapshot and the on-failure screenshot are kept (small, header-free). The log records URLs/statuses/console text only — never headers — and every entry is scrubbed by `sanitizeForLog` (`shared/helpers/log-sanitizer.ts`): exact-value redaction of secret-named env vars (`*_SECRET`, `*_PASSWORD`, `*_PRIVATE_KEY`, `*_TOKEN`, `*_API_KEY`, plus `CF_ACCESS_CLIENT_ID`; raw and URL-encoded forms) and pattern redaction of JWTs, Bearer tokens, basic-auth userinfo, and credential-named query params. Deliberately NOT redacted: 0x-prefixed 32-byte hex (tx/block hashes — core diagnostics; the funded pool keys come from env and are caught by exact match) and the RPC URLs (public, unkeyed endpoints in this repo — add them to `EXTRA_SENSITIVE_ENV_VARS` if a keyed provider URL is ever adopted). Sanitization runs before the per-entry length cap so a truncated secret can't dodge its exact-match needle. Don't reintroduce `trace`/`video` in config; for local debugging use `npx playwright test --trace on` per run, and treat gated-host traces as secrets (rotate the service token in the Cloudflare dashboard and update the `CF_ACCESS_CLIENT_*` GitHub secrets if one leaks). New fixture files must layer `contextWithDiagnostics` into their `context` override the way `base-test.ts` and both wallet fixtures do.

### Auth tests

- `IMAP_HOST`, `IMAP_PORT`, `IMAP_USER`, `IMAP_PASSWORD`, `OTP_FROM_EMAIL` — IMAP creds for OTP retrieval.
- `EMAIL_DOMAIN` (default `e2e.decentraland.org`) — domain used by `generateFreshEmail()` for new-user OTP signups. Each call returns `<prefix><hash>@<domain>` and the catch-all routes deliveries to `IMAP_USER`'s inbox.
- `TEST_USERNAME_PREFIX` (default `QA`) — prefix for the in-world username generated by `uniqueUsername()` in `tests/auth/helpers/test-user.ts`. Override to distinguish profiles created by a specific runner.
- `TEST_EMAIL_PREFIX` (default `qa-`) — prefix for the email local-part used by `generateFreshEmail()` in `tests/auth/helpers/otp-mailbox.ts`. Combine with a domain pointed at a personal Gmail (e.g. `EMAIL_DOMAIN=gmail.com` + `TEST_EMAIL_PREFIX=mike+qa-`) to route OTPs into a developer inbox via plus-addressing.
- `WEB_BASE_URL` (default `https://decentraland.org`) — dapp base URL; switch to `https://decentraland.zone` or `https://decentraland.today` to target development / staging (both CF-gated — see CF Access section above).
- `AUTH_SERVER_URL` — RequestPage tests broker `dcl_personal_sign` / `eth_sendTransaction` requests through this. Auto-derived as `auth-api.<host>` from `WEB_BASE_URL` (so a `.today` dapp run talks to `auth-api.decentraland.today`); set this only to point at a non-paired auth-api host.

### Marketplace tests

- The marketplace dapp shares the single `WEB_BASE_URL` host (see Auth tests above) — there is no separate `BASE_URL`. `getBaseUrl()` is the one host resolver for every project and helper.
- `MARKETPLACE_BASE_URL` — overrides `${WEB_BASE_URL}/marketplace/`. Trailing slash required.
- `MARKETPLACE_ENV` — `dev` to switch the dapp to Polygon Amoy / Sepolia (testnets) on the public `.org` host.
- `MARKETPLACE_API_BASE_URL` — explicit override for the marketplace-api host (used by helpers that query the indexer directly, e.g. `nft-indexer.ts`). Optional. Default behavior derives `marketplace-api.<host>` from `WEB_BASE_URL`'s host, with one legacy exception: `WEB_BASE_URL=https://decentraland.org` + `MARKETPLACE_ENV=dev` routes to `marketplace-api.decentraland.zone` (testnet indexer reached from the prod dapp via `?env=dev`). A run with `WEB_BASE_URL=https://decentraland.org` + `MARKETPLACE_ENV=prod` resolves to the production indexer (mainnet NFTs only); on-chain testnet specs targeting `MARKETPLACE_TEST_ITEM_*` will not find their items there. **`.today` is unsupported** for marketplace tests — the `.today` dapp reads from prod's marketplace-api (mainnet), so marketplace tests against `.today` would mix testnet expectations with mainnet data. `marketplaceApiBaseUrl()` throws when called with `WEB_BASE_URL=https://decentraland.today`, and the manual GitHub workflow rejects `environment=today` + `suite=marketplace[-onchain]` at the validation step. See `shared/helpers/marketplace-api.ts` for the full resolution rule.

### On-chain marketplace tests (testnet only — never use real-fund wallets)

- `WALLET_A_PRIVATE_KEY`, `WALLET_B_PRIVATE_KEY` — distinct EOAs, both funded with MANA on Polygon Amoy, both with ERC20 (MANA) approval to OffChainMarketplaceV2 (one-off setup).
- `POLYGON_AMOY_RPC_URL`, `SEPOLIA_RPC_URL` — RPC providers (defaults are public and rate-limited; use Alchemy/Infura for production).
- `MARKETPLACE_TEST_ITEM_CONTRACT`, `MARKETPLACE_TEST_ITEM_ID`, `MARKETPLACE_TEST_ITEM_TYPE` (`item` for primary, `nft` for secondary).
- `MARKETPLACE_TEST_LISTING_PRICE_MANA` (default `"1"`).

On-chain specs **self-skip** if any of these is missing or placeholder, via a `haveOnChainConfig` guard (see `buy-and-sell.spec.ts` for the pattern). Use `optionalEnv` at module level for the guard, then `requireEnv` inside the test body once the guard has confirmed presence.

`MARKETPLACE_TEST_GIFT_TOKEN_ID` (used by `nft-gift.spec.ts`) must be an actual tokenId owned by `WALLET_A` or `WALLET_B` on `MARKETPLACE_TEST_ITEM_CONTRACT` — DCL Collection tokenIds are packed 256-bit values (`itemId<<184 | issuedId`), not small integers, so picking one at random will revert with "nonexistent token". Run `node scripts/find-gift-token.mjs` from `web/` to discover a valid ID from current inventory; if neither wallet owns any NFTs on the contract (rare — would require the round-trip to have lost ownership), do a one-off primary buy via the marketplace dapp UI with one of the pool wallets to mint a fresh fixture.

**Conflict with `@web` RequestPage specs.** The `@on-chain` auth specs (`mana-donation`, `nft-gift`) require `AUTH_SERVER_URL=https://auth-api.decentraland.zone` so the dapp picks up testnet contracts. The `@web` RequestPage specs (`request-page.spec.ts`, `request-page-failure.spec.ts`) target the prod RequestPage and don't apply `withEnv(?env=dev)` to their goto — with `AUTH_SERVER_URL=.zone` set, they fail because the request is created on `.zone`'s auth-api but the `.org` dapp looks at `.org`'s auth-api ("request not available"). Naively routing the @web specs to `?env=dev` doesn't help either: the dev RequestPage signs `personal_sign` eagerly on page load (unlike prod which waits for user click), so `applyPersonalSignOverride` arrives too late and viem throws "unknown RPC error". Workaround: run the two test groups in separate shells with different `.env` values, or `unset AUTH_SERVER_URL` before `npm test` and re-export it before `npm run test:auth:onchain`. The `eth_sendTransaction` case in `request-page.spec.ts` doesn't have the signing race and could safely use `withEnv` if needed, but the file is kept consistent for clarity.

## Cross-platform handoff (`@cross`)

Currently skipped (`test.describe.skip`). When enabled:

1. The dapp's "Jump Into Decentraland" CTA writes `auth-token-bridge.txt`.
2. Path: `~/Library/Application Support/DecentralandLauncherLight/auth-token-bridge.txt` (macOS).
3. The Decentraland Launcher's `TokenFileAuthenticator` reads + deletes the file on startup.
4. Verification runs through `explorer/Tests/Tests/CrossPlatformVerificationTests.cs::TestExplorerIsInWorldFromTokenBridge`.

If those move, update `tests/auth/helpers/token-bridge.ts` or `tests/auth/helpers/explorer-runner.ts`.

## Pitfalls observed

- Quick-setup profile deployment is slow and high-variance on prod: after LET'S GO the button reads "DEPLOYING..." for 5-45s+ before the "Account is Ready!" interstitial appears, and the ToS checkbox's `check()` can take ~20s while the avatar preview loads. A clean single-phase web3 signup measures ~110s end-to-end. This is why the `web` project timeout is 240s and `QuickSetupPage.clickStartExploring` waits for the CTA with its own 120s budget — don't "optimize" either back down; every auth spec self-bootstraps through this screen, so a tight budget makes the whole suite flaky with timeouts scattered across unrelated steps.
- Calling `setupBroadcastWallet` after `page.goto` — silent no-op, broadcast layer never installs, dapp falls back to Web3Mock fakes.
- Asserting `page.waitForURL(/\/(success|status)/)` on the buy flow — passes on `/status` before any tx mines. Use `/success` only and combine with `waitForTransactionReceipt`.
- Adding a marketplace spec without `@marketplace` — runs under no project, silently skipped.
- Adding an on-chain spec without `@on-chain` — both projects exclude it, silently skipped. AND if the spec ends up running under `marketplace` (the off-chain project) due to a `grep` mismatch, parallel workers cause wallet-pool nonce reverts.
- Running `playwright test --project=marketplace-onchain` directly without `--workers=1` — when more than one on-chain spec exists, they race on the wallet pool. Always go through `npm run test:marketplace:onchain`.
- Same-EOA parallel txns revert on the contract's nonce check — the relayer's 2-min timeout doesn't serialize them. The wallet pool design depends on `--workers=1`.
- `test.setTimeout(...)` inside a test body does NOT extend back over the fixture phase — fixtures already ran under the project default. For fixture-heavy tests (e.g. `buy-and-sell.spec.ts`), set `test.describe.configure({ timeout: 420_000 })` at the top of the describe block.
- Reporting Amoy as the wallet's `eth_chainId` when the NFT is on Amoy flips the dapp's authorization saga to the direct-broadcast path (`eth_sendTransaction` → INSUFFICIENT_FUNDS without POL gas). Keep the wallet on Sepolia so the dapp uses the meta-tx path through transactions-server.
- Importing `test` from `@playwright/test` in a spec — works on `.org`, silently loses the CF Access route on `.zone`/`.today` and the run dies on blank pages with misleading locator timeouts. Import from `shared/fixtures/base-test.js` or a wallet fixture (ESLint blocks the bad import).
- A blank page / missing button on `.zone` with passing `.org` runs — check the browser console in the trace for CORS preflight errors on `cdn.decentraland.org` before debugging locators; that signature means a non-safelisted header is leaking onto cross-origin requests. Remember active `page.route()`s mask the problem (interception skips preflights), so it can appear only in specific specs or phases.
- Hard-coded sleeps — never. Use Playwright's auto-waiting or `waitForURL` / `waitFor` with explicit timeouts.
- Passing a bound method as a callback drops `this` and TypeErrors at runtime even though TS accepts it. The primary-mint test in `tests/marketplace/specs/buy-and-sell.spec.ts` now calls `authModal.authorizeAndSign(2_000)` directly inside the spec body — no callback indirection — but if a future helper takes a callback, always wrap: `intervalMs => authModal.authorizeAndSign(intervalMs)`.
- `chromium.launchPersistentContext` for a real MetaMask extension — not needed; the Synpress mock approach in `tests/auth/helpers/wallet.ts` covers all current cases without that complexity.

## Don't

- Don't import from `../explorer/Tests/`. The two stacks integrate only via `auth-token-bridge.txt` and the `dotnet test` shell-out from `tests/auth/helpers/explorer-runner.ts`.
- Don't share state between specs via module globals — Playwright runs files in parallel.
- Don't add a new POM in `tests/auth/pages/` or `tests/marketplace/pages/` without a header comment citing the testid → source-constant mapping (marketplace) or accessible-name → form-field mapping (auth).
- Don't put assertions inside POM action methods. Specs assert; methods interact.
- Don't unify the auth and marketplace authentication strategies. They're different on purpose.

## Adding a new dapp

The `tests/<dapp>/{specs,pages,helpers,fixtures}/` shape is symmetric. To add a third dapp (e.g. `account/`, `builder/`):

1. Create `tests/<newdapp>/{specs,pages,helpers,fixtures}/` with the same internal layout as `tests/marketplace/`.
2. Tag every spec describe with `@<newdapp>`.
3. Add a `<newdapp>` project entry in `playwright.config.ts` with matching `grep` and `baseURL`.
4. If the new dapp uses the same auth/wallet stack, reuse `injectAuthIdentity` + `setupBroadcastWallet` directly. If its tx flow differs (direct-broadcast, no relayer), document that explicitly at the top of the new dapp's first spec.
5. Update this file only if the new dapp introduces a load-bearing invariant. Routine additions belong in the README.
