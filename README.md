# Decentraland Automation

End-to-end UI automation across the Decentraland product surface — both the **desktop Explorer** (Unity client) and the **web dapp** (`decentraland.org`). The repo hosts two independent test stacks under one roof so they can share a single test identity, credentials, and tooling:

| Stack                    | Tech                                                                                 | Targets                                                                                                                                                 | README                                   |
| ------------------------ | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| [`explorer/`](explorer/) | C# / .NET 10 / NUnit / [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) | Decentraland Explorer **desktop client** (Unity) — login, in-world flows, panels, shortcuts                                                             | [explorer/README.md](explorer/README.md) |
| [`web/`](web/)           | TypeScript / [Playwright](https://playwright.dev/)                                   | Decentraland **web dapp** (`decentraland.org`, `/auth`, `/auth/quick-setup`), launcher download, and the cross-platform handoff into the desktop client | [web/README.md](web/README.md)           |

The two stacks are wired together through the `auth-token-bridge.txt` file: the dapp writes it after a successful web login, the desktop client reads + deletes it on launch to skip the login screen. The `@cross` Playwright tests are designed to verify the full chain (web login → bridge file → desktop launch → in-world).

## Shared at the root

- **`.env`** — IMAP credentials for OTP retrieval, loaded by both stacks. Copy from [`.env.example`](.env.example) and fill in real values. **Never commit this file.**
- **`.claude/`** — shared agents and skills.

To provision the BIP39 wallet identity used by all in-world tests (the `Category=InWorld` C# suite and the `@cross` Playwright suite), use `metaforge account create <name>` directly — see each stack's README for details.

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

Two GitHub Actions workflows for the web suite:

- **Web E2E (PR)** (`.github/workflows/web-e2e-pr.yml`) — runs automatically on every pull request that touches `web/**`. Executes `--project=web` (auth + landing, 12 tests) against `decentraland.org`. Skips drafts. Cancels older runs on the same PR when a new commit lands. Needs `IMAP_*` secrets only — `.org` isn't behind Cloudflare Access, so no CF tokens required for this workflow. (We don't run against `.zone` because its CF Access policies are scoped per-route and the available service token only authorizes `/auth/*` — investigate the infra side before flipping this to `.zone`.)
- **Web E2E (manual)** (`.github/workflows/web-e2e.yml`) — on-demand from **Actions → Web E2E (manual) → Run workflow**. Two inputs:

- **`environment`** — `org` (production, default) or `zone` (development). Sets `WEB_BASE_URL` and `BASE_URL` to `https://decentraland.<environment>`; the auth/landing suite reads the former (via `getBaseUrl()`), the marketplace suite reads the latter.
- **`suite`** — which bucket to run:

| Suite                 | Runs                                                                     | Notes                                                                                             |
| --------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------- |
| `all`                 | every `@web` test (auth + landing)                                       | default                                                                                           |
| `auth`                | new-user signup + recurrent-user login + cross-sites + RequestPage + ... | OTP test requires IMAP secrets                                                                    |
| `landing`             | launcher download CTA + future landing specs                             | no secrets needed                                                                                 |
| `marketplace`         | marketplace off-chain specs (browse, account, connect-wallet)            | no secrets needed                                                                                 |
| `marketplace-onchain` | marketplace buy-and-sell on Polygon Amoy                                 | requires `WALLET_A_PRIVATE_KEY`, `WALLET_B_PRIVATE_KEY`, and the `MARKETPLACE_TEST_ITEM_*` config |
| `cross`               | web → desktop handoff                                                    | currently `.skip`'d                                                                               |

**Required configuration for `auth`** — in repo Settings → Secrets and variables → Actions. The non-sensitive values (`IMAP_HOST`, `IMAP_PORT`, `IMAP_USER`, `OTP_FROM_EMAIL`) can go in either the **Secrets** or the **Variables** tab; the workflows read `vars.X || secrets.X`. `IMAP_PASSWORD` must be a Secret. Without these only `landing` and the wallet-mocked auth tests will pass.

**Additional secrets for `environment=zone`**: `CF_ACCESS_CLIENT_ID`, `CF_ACCESS_CLIENT_SECRET`. The dapp at `decentraland.zone` is gated behind Cloudflare Access — without these the browser will hit a CF login wall on the first navigation and the run will fail. The `*.api.decentraland.zone` hosts (auth-api, marketplace-api) are publicly reachable and don't need these headers. Not needed for `environment=org`.

**Additional secrets for `marketplace-onchain`**: `WALLET_A_PRIVATE_KEY`, `WALLET_B_PRIVATE_KEY` (testnet wallets funded with MANA on Polygon Amoy + ERC20 approval to OffChainMarketplaceV2 — one-time setup), and the test-item config: `MARKETPLACE_TEST_ITEM_CONTRACT`, `MARKETPLACE_TEST_ITEM_ID`, `MARKETPLACE_TEST_ITEM_TYPE`, optionally `MARKETPLACE_TEST_LISTING_PRICE_MANA`. Optional RPC overrides: `POLYGON_AMOY_RPC_URL`, `SEPOLIA_RPC_URL` (defaults are the public rate-limited endpoints).

Two workflows run the desktop (C#) InWorld suite, both delegating to the reusable `run-inworld-suite.yml`, which runs macOS on GitHub-hosted `macos-14` and Windows on the `win-gpu-t4-explorer` pool and tabulates both legs together:

- **InWorld (PR)** (`.github/workflows/inworld-pr.yml`) — on every pull request touching `explorer/Tests/**` or `explorer/ci/**`. Picks which fixtures to run by Roslyn reachability, so the size of a run follows what the branch touches, and comments the result on the PR. Skips drafts and fork PRs.
- **InWorld (main)** (`.github/workflows/inworld-main.yml`) — on every merge to `main` touching those same paths. Runs the whole `Category=InWorld` suite, so the wide run backs the scoped one the PR got. The result lands in the job summary.

macOS runs the whole selection on its one runner; Windows splits it across shards, planned in one place for every origin — see [explorer/ci/CLAUDE.md](explorer/ci/CLAUDE.md).

### Fixture infrastructure

The InWorld workflows can run against ephemeral fixture infrastructure managed by
[`explorer-e2e-infra`](https://github.com/decentraland/explorer-e2e-infra). The
The default remains `.org`. For automatic PR/main runs, set these Actions
repository or `e2e` environment variables:

```text
E2E_FIXTURE_PROVISION=true
E2E_FIXTURE_MODE=org
E2E_FIXTURE_MANAGER_ROLE_ARN=<IAM role ARN trusted for this repository/environment>
E2E_FIXTURE_MANAGER_FUNCTION_NAME=<e2e-fixture-manager Lambda name or ARN>
E2E_FIXTURE_MANAGER_AWS_REGION=us-east-1
E2E_FIXTURE_PROFILE=core-v1
E2E_FIXTURE_TTL_MINUTES=90
E2E_FIXTURE_SEED_VERSION=empty-bootstrap
```

For a manual run, use the `provision_fixture` checkbox in the **InWorld Suite**
workflow. When it is unchecked, the existing `org`/`local` behavior is kept;
when checked, the workflow provisions and destroys the fixture infrastructure
around the test legs. `fixture_mode=external` remains supported as a
compatibility alias for existing callers.

The reusable workflow assumes that role through GitHub Actions OIDC from the
`e2e` environment. Its trust policy should match
`repo:decentraland/explorer-automation:environment:e2e`, and the role should
only be able to invoke the fixture-manager Lambda. No SSH key or long-lived
AWS credential is needed in the repository. The Lambda creates the run-scoped
ECS task from the requested seed, polls it until ready, and destroys it after
the macOS and Windows legs finish; its scheduled sweep is the cleanup backstop
for canceled jobs.

The manager returns the run-scoped HTTPS fixture URL. The workflow passes it to
Explorer as `--realm <fixture-url>` and `--gateway-url <fixture-url>`; the same
origin exposes the realm `/about`, Catalyst content/lambdas, and the registry
gateway. `--base-domain` is not a Unity Explorer argument. Communications stay
offline because the first stack does not provision Archipelago or LiveKit.

The visual regression suite has its own reusable (`run-visual-suite.yml`), driven by **Manual Visual Tests** from the Actions tab and by the `/generate-baselines` PR comment. A Windows counterpart for *that* suite still needs the same GPU runner image wired up.

## Layout

```
explorer-automation/
├── explorer/         # C# / NUnit / AltTester (desktop client)
├── web/              # TS / Playwright (web dapp + cross handoff)
├── Makefile          # repo-wide entry points (run `make help`)
├── .env.example      # shared credential template
└── .claude/          # shared agents and skills
```
