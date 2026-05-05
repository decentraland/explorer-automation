# Explorer (Desktop) Automation Tests

UI automation tests for the Decentraland Explorer **desktop client** using [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) and NUnit.

For the web/dapp test stack see [../web/README.md](../web/README.md). For repo-wide context see [../README.md](../README.md).

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [AltTester Desktop](https://alttester.com/alttester/) (Pro license required)
- An instrumented Explorer build or the Unity Editor
- [MetaForge CLI](https://github.com/decentraland/metaforge) on your PATH
- A `.env` at the **repo root** populated from [../.env.example](../.env.example) — required only for the **Auth** suite (IMAP credentials to fetch OTP codes); the **InWorld** suite does not read it.

All commands below assume you run them from the repo root.

## Test categories

Fixtures are tagged with NUnit `[Category]` so you can run them in isolation:

| Category | Fixtures | Starts from | Consumes Thirdweb OTP? |
|---|---|---|---|
| `Auth` | `EmailOtpLoginTests`, `EmailOtpLoginWithNewsletterTests`, `EmailOtpRecurrentLoginTests` | logged-out (cache cleared) | yes — IMAP fetches the code |
| `InWorld` | `BackpackEmotesTests`, `ExplorePanelTests`, `ShortcutsTests` | in-world via a pre-cached identity | no |

Within each category, fixtures execute in their declared `[Order]`.

## Running Tests

### Run everything in one shot

Auth tests run first (Order 1–3) and leave a Thirdweb-cached identity in-world; InWorld tests pick up from there.

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

Adding a new visual test:

```bash
cd scenes
npm run new-scene -- my-feature             # scaffolds packages/my-feature + Tests/Tests/Visual/MyFeatureFixture.cs
# edit packages/my-feature/src/index.ts to render what you want to snapshot
# edit Tests/Tests/Visual/MyFeatureFixture.cs to add Frame.WaitForStable + Snapshot.AssertMatchesBaseline
```

Recording a baseline (manual; CI never auto-records):

```bash
metaforge explorer test dev --filter "Category=Visual&FullyQualifiedName~MyFeatureFixture" --record-baselines
git add Tests/Baselines/MyFeatureFixture/
```

See [SNAPSHOTS.md](SNAPSHOTS.md) for the full reference: snapshot API, modes, tolerance tuning, Allure attachments, baseline storage, known caveats.
