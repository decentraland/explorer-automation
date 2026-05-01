# Web Automation Tests

[Playwright](https://playwright.dev/) UI automation for the Decentraland **web dapp** at `https://decentraland.org` (and the cross-platform handoff into the desktop client). For the desktop test stack see [../explorer/README.md](../explorer/README.md). For repo-wide context see [../README.md](../README.md).

## Specs

| Tag | Spec | Tests | Notes |
|---|---|---|---|
| `@web` | [tests/auth-email-otp.spec.ts](tests/auth-email-otp.spec.ts) | new user (no newsletter) / new user (with newsletter) / recurrent user | mirrors the C# `Category=Auth` fixtures |
| `@web` | [tests/download.spec.ts](tests/download.spec.ts) | clicking "DOWNLOAD FOR <platform>" hero CTA fires a `download` event | no auth needed |
| `@cross` | [tests/web-to-inworld-handoff.spec.ts](tests/web-to-inworld-handoff.spec.ts) | web login → bridge file → desktop client lands in-world | **currently `test.describe.skip`** until the dapp's "Jump Into Decentraland" CTA is captured via codegen |

## Prerequisites

- **Node.js 20+**
- **Chromium** for Playwright: `npx playwright install chromium`
- A `.env` at the **repo root** populated from [../.env.example](../.env.example) (IMAP credentials for OTP retrieval)
- The `recurrent user` test additionally requires `EXPLORER_IMAP_USER` (no `+alias`) to already be a registered Decentraland account — same precondition as the C# `EmailOtpRecurrentLoginTests`. Sign it up once via the regular new-user flow.

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
npm test               # all @web tests (the 3 OTP specs + download)
npm run test:headed    # same, with a visible browser
npm run test:ui        # Playwright UI mode (interactive debug)

npm run typecheck      # tsc --noEmit
npm run report         # open the last HTML report
```

For ad-hoc filtering, use Playwright directly:

```bash
npx playwright test -g "recurrent"
npx playwright test tests/download.spec.ts
```

### OTP rate limits

Thirdweb rate-limits OTP sends per recipient (~3/min). The three OTP tests run sequentially and each uses a fresh `+alias`, so a single full run is fine. Back-to-back runs may hit the limit. For CI / heavy loops, populate `EXPLORER_ALTERNATE_EMAILS` in `.env` with addresses that route to the same inbox (Gmail aliases or domain forwards).

### Watching OTPs during local debugging

When recording flows with `playwright codegen` or running tests interactively, [`scripts/watch-otp.mjs`](scripts/watch-otp.mjs) tails the inbox and prints new OTPs in real time:

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
│   ├── token-bridge.ts    # platform-aware path + wait/read/remove
│   └── explorer-runner.ts # spawn Explorer, shell out to dotnet test
├── pages/
│   ├── LandingPage.ts     # https://decentraland.org → Sign In
│   ├── AuthPage.ts        # /auth → email submit + OTP entry (keyboard.press per box)
│   ├── QuickSetupPage.ts  # /auth/quick-setup → username / newsletter / ToS / LET'S GO
│   └── HomePage.ts        # post-login homepage + DOWNLOAD CTA
├── scripts/
│   └── watch-otp.mjs      # tail the inbox during codegen / debugging
└── tests/
    ├── auth-email-otp.spec.ts          # @web — 3 tests
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
