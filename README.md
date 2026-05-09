# Decentraland Automation

End-to-end UI automation across the Decentraland product surface — both the **desktop Explorer** (Unity client) and the **web dapp** (`decentraland.org`). The repo hosts two independent test stacks under one roof so they can share a single test identity, credentials, and tooling:

| Stack | Tech | Targets | README |
|---|---|---|---|
| [`explorer/`](explorer/) | C# / .NET 10 / NUnit / [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) | Decentraland Explorer **desktop client** (Unity) — login, in-world flows, panels, shortcuts | [explorer/README.md](explorer/README.md) |
| [`web/`](web/) | TypeScript / [Playwright](https://playwright.dev/) | Decentraland **web dapp** (`decentraland.org`, `/auth`, `/auth/quick-setup`), launcher download, and the cross-platform handoff into the desktop client | [web/README.md](web/README.md) |

The two stacks are wired together through the `auth-token-bridge.txt` file: the dapp writes it after a successful web login, the desktop client reads + deletes it on launch to skip the login screen. The `@cross` Playwright tests are designed to verify the full chain (web login → bridge file → desktop launch → in-world).

## Shared at the root

- **`.env`** — IMAP credentials for OTP retrieval, loaded by both stacks. Copy from [`.env.example`](.env.example) and fill in real values. **Never commit this file.**
- **[`scripts/setup-test-identity.sh`](scripts/setup-test-identity.sh)** — provisions the BIP39 wallet identity used by all in-world tests (the `Category=InWorld` C# suite and the `@cross` Playwright suite). Idempotent.
- **`.claude/`** — shared agents and skills.

## Quick start

```bash
# Clone, then:
cp .env.example .env       # fill in IMAP credentials

# Desktop suite (C#)
dotnet build explorer/Tests/
metaforge explorer test --filter "Category=InWorld"

# Web suite (TypeScript)
cd web && npm install && npx playwright install chromium
npm test
```

See each stack's README for the full prerequisite list, run modes, and troubleshooting.

## Continuous integration

The web suite can be run on demand via GitHub Actions: **Actions → Web E2E (manual) → Run workflow**. Two inputs:

- **`environment`** — `org` (production, default) or `zone` (development). Sets `WEB_BASE_URL=https://decentraland.<environment>`, which `web/helpers/env.ts::getBaseUrl()` propagates to the Playwright `baseURL`, every spec's `REDIRECT_TO`, and the wallet helper's auth-page URL.
- **`suite`** — which bucket to run:

| Suite | Runs | Notes |
|---|---|---|
| `all` | every `@web` test | default |
| `auth` | new-user signup + recurrent-user login (web3 + OTP) | OTP test requires IMAP secrets |
| `download` | launcher download CTA | no secrets needed |
| `cross` | web → desktop handoff | currently `.skip`'d |

**Required GitHub Action secrets** (one-time, in repo Settings → Secrets and variables → Actions): `IMAP_HOST`, `IMAP_PORT`, `IMAP_USER`, `IMAP_PASSWORD`, `IMAP_FROM_USER`. Without these only `download` and the wallet-mocked auth tests will pass.

**Additional secrets for `environment=zone`**: `CF_ACCESS_CLIENT_ID`, `CF_ACCESS_CLIENT_SECRET`. The `.zone` hosts are gated behind Cloudflare Access — without these the workflow runs against `.zone` will hit a CF login wall and fail. Not needed for `environment=org`.

The desktop (C#) suite is not yet wired into CI — it needs a self-hosted Windows GPU runner with the instrumented Explorer client + AltTester Desktop on port 13000.

## Layout

```
explorer-automation/
├── explorer/                       # C# / NUnit / AltTester (desktop client)
├── web/                            # TS / Playwright (web dapp + cross handoff)
├── scripts/setup-test-identity.sh  # shared identity provisioning
├── .env.example                    # shared credential template
└── .claude/                        # shared agents and skills
```
