# Explorer Automation Tests

UI automation tests for the Decentraland Explorer client using [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) and NUnit.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [AltTester Desktop](https://alttester.com/alttester/) (Pro license required)
- An instrumented Explorer build or the Unity Editor
- [MetaForge CLI](https://github.com/decentraland/metaforge) on your PATH
- A `.env` at the repo root populated from [.env.example](.env.example) — required only for the **Auth** suite (IMAP credentials to fetch OTP codes); the **InWorld** suite does not read it.

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

Uses a metaforge-managed test identity so the Explorer skips the login screen entirely. **Run [`scripts/setup-test-identity.sh`](scripts/setup-test-identity.sh) once first** — it creates a BIP39 wallet, registers the identity with the Decentraland auth API, and writes the auth token bridge so the launcher auto-logs in.

```bash
# One-time (or whenever the token bridge is missing)
scripts/setup-test-identity.sh

# Every run from then on
metaforge explorer run -- --alttester              # NOTE: no --clear
metaforge explorer test --filter "Category=InWorld"
```

The script is idempotent: if the account already exists it just re-issues the token bridge.

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
   dotnet test Tests/ --logger "console;verbosity=detailed"
   dotnet test Tests/ --filter "Category=InWorld"
   ```

## Project Structure

```
ExplorerAutomation.sln
Tests/
├── Common/
│   └── Reporter.cs                 # Logging and Allure screenshot helpers
├── Tests/
│   ├── BaseTest.cs                 # Driver setup, view initialization, EnsureInWorld
│   ├── ExplorePanelTests.cs        # Explore panel sidebar and tab tests
│   └── ShortcutsTests.cs           # Keyboard shortcut tests
├── Views/
│   ├── BaseView.cs                 # Abstract base: click, wait, find, text helpers
│   ├── AuthenticationMainScreenView.cs
│   ├── SplashView.cs
│   ├── LoadingScreenView.cs
│   ├── MainMenuView.cs             # Sidebar buttons
│   ├── ExplorePanelView.cs         # Panel container + tab switching
│   └── ExplorePanelSections/       # Sections specific to the Explore Panel
│       ├── BaseSection.cs
│       ├── EventsSection.cs
│       ├── PlacesSection.cs
│       ├── CommunitiesSection.cs
│       ├── NavmapSection.cs
│       ├── BackpackSection.cs
│       ├── GallerySection.cs
│       └── SettingsSection.cs
└── GlobalUsings.cs
```

## Architecture

The project follows the **Page Object Model (POM)** pattern.

### View hierarchy

```
BaseView (abstract)
  ├── AuthenticationMainScreenView
  ├── SplashScreenView
  ├── LoadingScreenView
  ├── MainMenuView
  └── ExplorePanelView
        └── Sections (BaseSection)
              ├── EventsSection
              ├── PlacesSection
              ├── CommunitiesSection
              ├── NavmapSection
              ├── BackpackSection
              ├── GallerySection
              └── SettingsSection
```

- **BaseView** provides reusable interaction methods (`ClickObject`, `WaitForObject`, `WaitForObjectNotBePresent`, `IsObjectPresent`, `SetText`, `GetText`) with built-in timeout handling and Allure step tracking.
- **BaseSection** extends `BaseView` with a section locator and visibility/wait helpers. Section classes live under `Views/ExplorePanelSections/` since they are specific to the Explore Panel.
- **View classes** encapsulate UI locators as `(By, string)` tuples and expose high-level actions. Most locators use `By.ID` with UUIDs for stability.

### Test lifecycle

`BaseTest` manages the full lifecycle:

1. **OneTimeSetUp** — Connects `AltDriver` to AltTester Desktop, creates all view objects, runs `EnsureInWorld()` (waits through splash, authentication, and loading screens).
2. **SetUp** — Presses Escape to clear any open panels.
3. **Test** — Uses pre-initialized view properties (`MainMenuView`, `ExplorePanelView`, etc.).
4. **TearDown** — Takes a screenshot on failure.
5. **OneTimeTearDown** — Disconnects the driver.

### Reporting

- **Console:** Timestamped logs via `Reporter.Log()`.
- **Allure:** `[AllureStep]`, `[AllureSuite]`, `[AllureTest]` attributes generate rich HTML reports.
- **Screenshots:** Captured automatically on test failure and attached to Allure results.

## Adding a New Test

1. Create a new test class in `Tests/` that inherits from `BaseTest`.
2. Use the pre-initialized view properties — do not create new view instances in tests.
3. Add `[TestFixture]` and `[AllureSuite("...")]` attributes.

```csharp
[TestFixture]
[AllureSuite("My Feature Tests")]
public class MyFeatureTests : BaseTest
{
    [Test]
    [AllureTest("Verify something works")]
    public void TestSomethingWorks()
    {
        MainMenuView.ClickEvents();
        ExplorePanelView.WaitForPanelOpen();
        Assert.That(ExplorePanelView.Events.IsSectionVisible(), Is.True);
    }
}
```

## Adding a New View

1. Create a new class in `Views/` inheriting from `BaseView` (or `BaseSection` in `Views/ExplorePanelSections/` for Explore Panel sections).
2. Define locators as `private readonly (By, string)` tuples.
3. Add the view as a property in `BaseTest` and initialize it in `InitializeViews()`.

See [CODING_STANDARDS.md](CODING_STANDARDS.md) for detailed conventions.
