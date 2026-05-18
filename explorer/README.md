# Explorer (Desktop) Automation Tests

UI automation tests for the Decentraland Explorer **desktop client** using [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) and NUnit.

For the web/dapp test stack see [../web/README.md](../web/README.md). For repo-wide context see [../README.md](../README.md).

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [AltTester Desktop](https://alttester.com/alttester/) — get a free trial license at https://alttester.com/tools
- An instrumented Explorer build or the Unity Editor
- [MetaForge CLI](https://github.com/decentraland/metaforge) on your PATH (visual mode requires **v2.1.2+**)
- A `.env` at the **repo root** populated from [../.env.example](../.env.example) — required only for the **Auth** suite (IMAP credentials to fetch OTP codes); the **InWorld** and **Visual** suites do not read it.

All commands below assume you run them from the repo root.

## First-time setup

```bash
# Install metaforge (or update an existing install with `mf update`)
# macOS
/bin/bash -c "$(curl -fsSL https://explorer-artifacts.decentraland.zone/tools/install.sh)"
# Windows (PowerShell)
iex (irm https://explorer-artifacts.decentraland.zone/tools/install.ps1)

# Store your AltTester Desktop license key (one-time)
mf explorer test --set-license YOUR_KEY

# Create + log in with a test wallet (one-time; reused by InWorld + Visual suites)
mf account create dev
mf account login dev
```

## Test categories

Fixtures are tagged with NUnit `[Category]` so you can run them in isolation:

| Category | Fixtures | Starts from | Consumes Thirdweb OTP? |
|---|---|---|---|
| `Auth` | `EmailOtpLoginTests`, `EmailOtpLoginWithNewsletterTests`, `EmailOtpRecurrentLoginTests` | logged-out (cache cleared) | yes — IMAP fetches the code |
| `InWorld` | `BackpackEmotesTests`, `ExplorePanelTests`, `ShortcutsTests` | in-world via a pre-cached identity | no |
| `Visual` | per-scene fixtures under `Tests/Tests/Visual/` (`CoreFixture`, `MaterialsFixture`, `GltfFixture`, `UiFixture`, …) | host server + hot-reloaded test scenes | no |

Within each category, fixtures execute in their declared `[Order]`.

### Fixture ordering invariant

The `Auth` fixtures (`Order` ≥ 1000) **must run last in the assembly.** They inherit `LoggedOutAuthBaseTest`, which signs the player out and leaves the Explorer on the LoginSelection screen. `BaseTest.EnsureInWorld` can recover from splash, cached-account, or already-in-world — but **not** from a fully logged-out state. Any non-Auth fixture that runs after Auth will fail in `OneTimeSetUp`.

NUnit quirk that makes this fragile: fixtures **with** `[Order]` run first in numeric order, then fixtures **without** `[Order]` run last in undefined order. So just giving Auth a high Order isn't enough — every other fixture must also carry an `[Order]` lower than 1000, otherwise it falls into the "unordered" bucket and runs *after* Auth.

**Rule when adding a new fixture:** annotate it with `[Order(N)]` where `N < 1000`. Current allocation: in-world fixtures `10–19`, visual fixtures `20–29`, Auth `1000+`. Pick the next free number in the relevant band.

## Running Tests

### Run everything in one shot

InWorld fixtures run first (Order 10–19) and leave the player in-world; Auth fixtures run last (Order 1000+) because they sign out — running them in any other position would leave the Explorer at LoginSelection and break every subsequent fixture's `EnsureInWorld`.

```bash
metaforge explorer run --clear -- --alttester     # logged-out launch
metaforge explorer test                            # all fixtures, ordered
```

Use this for the full smoke run when you don't mind burning OTP attempts.

### Auth suite only (consumes OTP)

Tests the email + OTP login flow end-to-end. Requires a clean cache so the client boots into the LoginSelection screen.

```bash
metaforge explorer run --clear -- --alttester
metaforge explorer test --filter "Category=Auth"
```

### InWorld suite only (no OTP, fast iteration)

Uses a metaforge-managed test identity so the Explorer skips the login screen entirely. **Provision the identity once first** with `metaforge account create <name>` — it creates a BIP39 wallet, registers the identity with the Decentraland auth API, and writes the auth token bridge so the launcher auto-logs in. The same identity is reused by the `@cross` Playwright tests in [`../web/`](../web).

```bash
# One-time
metaforge account create dcl-e2e-inworld

# Every run from then on
metaforge explorer run -- --alttester              # NOTE: no --clear
metaforge explorer test --filter "Category=InWorld"
```

### Targeted filters

NUnit's `--filter` syntax works with `Category=…`, `FullyQualifiedName~…`, `Name=…`, and boolean OR with `|`:

```bash
# Just the recurrent-user login
metaforge explorer test --filter "FullyQualifiedName~EmailOtpRecurrent"

# All emote tests + the recurrent login (e.g. for debugging the chain)
metaforge explorer test --filter "FullyQualifiedName~EmailOtpRecurrent|FullyQualifiedName~BackpackEmotes"

# A single test method
metaforge explorer test --filter "Name=TestUnequipAndEquipAllEmoteSlots"
```

### Manual (without metaforge)

If you're driving the Explorer + AltTester yourself:

1. Launch an instrumented Explorer with `--alttester` (build) or click **Play in Editor** under `AltTester > AltTester Editor` (editor).
2. Start AltTester Desktop and wait for the connection.
3. Run the tests:
   ```bash
   dotnet test explorer/Tests/ --logger "console;verbosity=detailed"
   dotnet test explorer/Tests/ --filter "Category=InWorld"
   ```

## Project Structure

```
explorer/
├── ExplorerAutomation.sln
├── MetaForge.TestLogger/              # custom NUnit logger consumed by metaforge
└── Tests/
    ├── Common/                        # Locatable / Readable / Clickable / Writable + Reporter
    ├── Tests/                         # NUnit fixtures (BaseTest + per-feature classes)
    ├── Views/                         # POM views (BaseView, BaseSection, etc.)
    │   └── ExplorePanelSections/      # Tab/section views for the explore panel
    └── GlobalUsings.cs
```

## Architecture

The project follows the **Page Object Model (POM)** pattern.

### Layers

- **Interaction primitives** (`Tests/Common/`) — `Locatable`, `Readable`, `Clickable`, `Writable` records wrap AltTester locators with timeout handling and Allure step tracking.
- **Views** (`Tests/Views/`) — typed page objects composed of primitives. Top-level views are registered in `ViewContainer`; sections live under `ExplorePanelSections/`.
- **Tests** (`Tests/Tests/`) — NUnit fixtures inheriting `BaseTest`, accessing views via the `Views` property.

### Test lifecycle

`BaseTest` manages the full lifecycle:

1. **OneTimeSetUp** — runs `EnsureInWorld()` (waits through splash, authentication, and loading screens).
2. **SetUp** — Presses Escape to clear any open panels.
3. **Test** — Uses views from `Views.X` (e.g. `Views.ExplorePanel`, `Views.MainMenu`).
4. **TearDown** — Takes a screenshot on failure.
5. **OneTimeTearDown** — Disconnects the driver.

`GlobalSetup` runs once for the whole assembly: connects `AltDriver` to AltTester Desktop at `127.0.0.1:13000`, initializes `ViewContainer`, sets up the Unity log listener.

### Reporting

- **Console:** Timestamped logs via `Reporter.Log()`.
- **Allure:** `[AllureStep]`, `[AllureSuite]`, `[AllureTest]` attributes generate rich HTML reports.
- **Screenshots:** Captured automatically on test failure and attached to Allure results.

## Adding tests / views

Use the project skills:

- **`view-writer`** — for any new view class, element, section, or helper.
- **`test-writer`** — for any new test class or test method.

Both skills live at `../.claude/skills/` and codify the full conventions (region layout, naming, lifecycle, locator discovery via the `alttester-explorer` agent).

## Visual Regression Testing

Pixel-diff tests run against custom SDK7 scenes hosted out of [`scenes/`](scenes). The host server stays up across many test invocations; each fixture's scene gets hot-reloaded into it on demand.

Two-command workflow:

```bash
metaforge explorer server start                                  # spawn the host (detaches)
metaforge explorer test dev --filter "Category=Visual"           # build scenes, launch Explorer, run
```

Adding a new visual test (run from the repo root):

```bash
make scenes-new-scene NAME=my-feature        # scaffolds scenes/packages/my-feature + Tests/Tests/Visual/MyFeatureFixture.cs
# edit scenes/packages/my-feature/src/index.ts to render what you want to snapshot
# edit Tests/Tests/Visual/MyFeatureFixture.cs to add Frame.WaitForStable + Snapshot.AssertMatchesBaseline
```

Recording a baseline:

**The canonical path is CI.** Comment `/generate-baselines` on your explorer-automation PR. The `Generate Baselines` workflow runs the suite with `--record-baselines` against a deterministic Explorer build, then auto-commits the regenerated PNGs back to your PR branch as `github-actions[bot]`.

Why CI rather than your laptop: GPU model, font subpixel hinting, color profile and OS version all affect rendered pixels. A baseline recorded on a dev machine can diverge from what CI renders, and every downstream unity-explorer PR would then fail visual regression against a baseline only your machine could reproduce. CI runs on a deterministic macOS-14 runner, so its baselines are the only ones the rest of the pipeline can trust.

You can still record locally during iteration to sanity-check the *fixture* (does it set up correctly, does `Frame.WaitForStable` settle?):

```bash
metaforge explorer test dev --filter "Category=Visual&FullyQualifiedName~MyFeatureFixture" --record-baselines
# Inspect the rendered PNG visually to confirm the fixture works,
# then DISCARD the local files — do NOT commit them.
git checkout -- Tests/Baselines/MyFeatureFixture/
```

Direct human commits to `Tests/Baselines/**/*.png` should be flagged at review. Only `github-actions[bot]` commits from `/generate-baselines` should land.

### Merge order with paired unity-explorer PRs

If your baseline change is paired with a unity-explorer PR (i.e. you used the matching branch name in both repos so `/visual-tests` picks up your tests, and `/generate-baselines` records against your Explorer build), **merge the unity-explorer PR first.**

Why: explorer-automation main is the baseline of record for every open unity-explorer PR. The moment a baseline change lands here, every unity-explorer PR re-runs visual tests against the new pixels — which only the paired Explorer build produces. Merging explorer-automation first breaks every other in-flight PR until the paired Explorer change lands.

The safe order is:
1. Get both PRs reviewed.
2. Merge the unity-explorer PR.
3. Once it's on `dev` and a successful Unity Cloud Build exists for that SHA, merge the explorer-automation PR.

If the baseline change is *standalone* (no Explorer change required — e.g. tightening tolerance on an existing test, recording for a brand-new scene that doesn't depend on Explorer changes), this doesn't apply and you can merge in any order.

See [SNAPSHOTS.md](SNAPSHOTS.md) for the full reference: snapshot API, modes, tolerance tuning, Allure attachments, baseline storage, known caveats.

### Example: running the visual suite locally end-to-end

A full local run from a clean checkout, against the `chore/visual-test-app-args` Explorer build:

```bash
# 1. Install repo dependencies (metaforge, .NET toolchain checks, scenes deps)
make install

# 2. One-time: provision a test wallet identity (skip if you already have one)
metaforge account create dev

# 3. Log in with an existing account (interactive picker if multiple exist)
metaforge account login

# 4. Start the scenes host server (detaches; stays up across runs)
metaforge explorer server start

# 5. Run the visual suite against the target Explorer branch
#    Expected: report is green, all tests pass.
metaforge explorer test chore/visual-test-app-args --filter "Category=Visual"

# 6. Stop the host server when you're done
metaforge explorer server stop
```
