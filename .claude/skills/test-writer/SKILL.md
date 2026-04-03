---
name: test-writer
description: Writes and modifies NUnit test classes (Tests/Tests/) for the Decentraland Explorer UI automation test suite. Trigger this skill whenever the user wants to create a new test class, add test methods to an existing class, modify test logic, or write test scenarios for any Explorer feature. Even if the user doesn't say "test" explicitly, trigger when they describe verifying UI behavior, checking that a panel opens/closes, asserting element state, or automating a user flow. If the test requires views, elements, or helper methods that don't exist yet, invoke the view-writer skill to create them before writing the test. Do NOT trigger for creating or modifying view classes (use view-writer), changing test infrastructure (BaseTest, GlobalSetup), or modifying Common/ primitives.
---

# Test Writer

Write and modify NUnit test classes for the Decentraland Explorer UI automation tests (AltTester SDK 2.3.0, NUnit 4, Allure reporting, C# 14).

## Architecture

Tests live in `Tests/Tests/` and follow the Page Object Model pattern. Tests never interact with raw AltTester locators — they only call methods on views.

**Layers:**
1. **Tests** (`Tests/Tests/`) — NUnit fixtures that orchestrate user flows by calling view methods
2. **Views** (`Tests/Views/`) — Page objects wrapping UI elements (managed by the `view-writer` skill)
3. **Primitives** (`Tests/Common/`) — `Locatable`, `Readable`, `Clickable`, `Writable` records

Tests access views through `Views` (a `ViewContainer` singleton), e.g. `Views.ExplorePanel.Events.WaitFor()`.

## What BaseTest Provides

All test classes inherit `BaseTest`, which provides:

**Properties:**
- `Views` — access to all registered views via `ViewContainer.Instance`
- `AltDriver` — the raw AltTester driver (rarely needed in tests)

**Lifecycle (automatic):**
- `[OneTimeSetUp]` — ensures the player is in-world (handles splash → auth → loading)
- `[SetUp]` — logs the test name and presses Escape to close any open popups
- `[TearDown]` — takes a screenshot on failure

**Helper methods:**
- `Wait(double seconds)` — brief pause for animations. Use sparingly.
- `PressKey(AltKeyCode keyCode, float delay = 0.5f)` — press a keyboard key
- `PressEscape()` — press Escape (shorthand for `PressKey(AltKeyCode.Escape)`)

Because `[SetUp]` already presses Escape, each test starts with all panels closed and the main HUD visible.

## Writing a Test Class

### File template

```csharp
namespace ExplorerAutomation.Tests.Tests;

[AllureSuite("Feature Name Tests")]
public class FeatureNameTests : BaseTest
{
    [Test]
    public void TestActionSubject()
    {
        // test body
    }
}
```

Every test class needs:
- **Namespace**: `ExplorerAutomation.Tests.Tests`
- **`[AllureSuite("...")]`**: descriptive suite name for Allure grouping
- **Inherits `BaseTest`**: gives you `Views`, helpers, and lifecycle
- No per-file usings needed — `GlobalUsings.cs` covers NUnit, Allure, AltTester, Views, Common

### Naming

| Element | Convention | Example |
|---|---|---|
| File | `{Feature}Tests.cs` | `ExplorePanelTests.cs` |
| Class | `{Feature}Tests` | `ExplorePanelTests` |
| Test method | `Test{Action}{Subject}` | `TestOpenEventsFromSidebar` |

Group related tests into one class per feature area. Don't make a class per test.

## Writing Test Methods

### The standard flow

Most tests follow this pattern:

```csharp
[Test]
public void TestOpenEventsFromSidebar()
{
    // 1. Navigate — open the target panel/screen
    Views.MainMenu.EventsButton.Click();

    // 2. Verify — wait for expected state
    Views.ExplorePanel.Events.WaitFor();

    // 3. (Optional) Interact — perform actions within the panel
    // ...

    // 4. Clean up — close the panel so the next test starts clean
    Views.ExplorePanel.CloseButton.Click();
    Views.ExplorePanel.WaitForGone();
}
```

The key phases are: **Navigate → Verify → Interact → Clean up**. Not every test needs all phases — a simple "does it open?" test might skip Interact.

### Interaction patterns

**Opening panels** — via sidebar buttons or keyboard shortcuts:
```csharp
Views.MainMenu.EventsButton.Click();     // sidebar button
PressKey(AltKeyCode.X);                   // keyboard shortcut
```

**Waiting for UI state:**
```csharp
Views.ExplorePanel.Events.WaitFor();      // wait up to 20s for element to appear
Views.ExplorePanel.WaitForGone();         // wait for element to disappear
```

**Checking presence without waiting** (for conditional logic):
```csharp
if (Views.ExplorePanel.Backpack.Emotes.Slots[0].UnequipButton.IsPresent())
{
    // slot has an emote equipped
}
```

**Typing into fields:**
```csharp
Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");
```

**Reading text:**
```csharp
var placeName = Views.ExplorePanel.Places.Cards[0].PlaceName.GetText();
```

**Calling view helper methods** (for multi-step operations):
```csharp
Views.ExplorePanel.Backpack.Emotes.UnequipAll();
Views.ExplorePanel.Backpack.Emotes.SetEmote(slotIndex: 0, gridIndex: 0);
```

**Closing panels:**
```csharp
Views.ExplorePanel.CloseButton.Click();   // explicit close button
PressEscape();                             // Escape key
```

### Assertions

Use NUnit's `Assert.That()` with descriptive failure messages:

```csharp
Assert.That(Views.ExplorePanel.Events.IsPresent(), Is.True,
    "Events section should be visible after pressing X");
```

Not every test needs explicit assertions. If `WaitFor()` succeeds, the element appeared — that's an implicit assertion (it throws on timeout). Use `Assert.That` when checking boolean conditions or comparing values.

### Reporting

Use `Reporter.Log()` to annotate significant steps. These show up in the Allure report and help debug failures:

```csharp
Reporter.Log("Events tab opened successfully");
Reporter.Log($"Set emote grid item {gridIndex} to slot {slotIndex}");
```

Log at meaningful checkpoints, not every line. A test with 3-5 log statements is typical.

### Waits

Timing-sensitive operations (animations, content loading) sometimes need a brief pause:

```csharp
Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");
Wait(2);  // wait for search results to filter
```

Use `Wait()` only when there's no element to wait on. If you can wait for a specific element instead (e.g. a loading indicator disappearing), that's always better.

## Test Complexity Spectrum

### Simple: open/close verification

```csharp
[Test]
public void TestOpenPlacesFromSidebar()
{
    Views.MainMenu.PlacesButton.Click();
    Views.ExplorePanel.Places.WaitFor();

    Views.ExplorePanel.CloseButton.Click();
    Views.ExplorePanel.WaitForGone();
}
```

### Medium: multi-step navigation

```csharp
[Test]
public void TestSwitchBetweenAllTabs()
{
    Views.MainMenu.EventsButton.Click();
    Views.ExplorePanel.WaitFor();

    Views.ExplorePanel.PlacesTabButton.Click();
    Views.ExplorePanel.Places.WaitFor();
    Reporter.Log("Places tab opened successfully");

    Views.ExplorePanel.CommunitiesTabButton.Click();
    Views.ExplorePanel.Communities.WaitFor();
    Reporter.Log("Communities tab opened successfully");

    Views.ExplorePanel.CloseButton.Click();
    Views.ExplorePanel.WaitForGone();
}
```

### Complex: data manipulation with helpers

```csharp
[Test]
public void TestSearchAndEquipFistPump()
{
    Views.MainMenu.BackpackButton.Click();
    Views.ExplorePanel.WaitFor();
    Views.ExplorePanel.Backpack.EmotesTabButton.Click();

    Views.ExplorePanel.Backpack.SearchBar.SetText("Fist Pump");
    Wait(2);

    Views.ExplorePanel.Backpack.Emotes.UnequipEmoteIfPresent(0);
    Views.ExplorePanel.Backpack.Emotes.SetEmote(0, 0);

    Reporter.Log("Fist Pump equipped to slot 0");

    Views.ExplorePanel.CloseButton.Click();
    Views.ExplorePanel.WaitForGone();
}
```

## When Views Are Missing

If a test requires a view, element, section, or helper method that doesn't exist yet, **invoke the `view-writer` skill** to create it before writing the test. This keeps the POM layer and tests cleanly separated.

Common scenarios that need view-writer:
- **New panel/screen** — e.g. testing a settings panel that has no `SettingsView` yet
- **Missing element** — e.g. a button in an existing view that hasn't been added
- **Missing section** — e.g. a new tab in the explore panel
- **Missing helper** — e.g. a multi-step operation that should be a view method rather than inline test code

After the view-writer creates the required views, write the test using the newly available elements.

### Deciding what belongs in a view vs. a test

**Put it in the view** (via view-writer) if:
- It's a reusable multi-step interaction (e.g. "equip an emote to a slot")
- Multiple tests would need the same sequence
- It operates on a view's internal elements

**Keep it in the test** if:
- It's a one-off flow specific to this test scenario
- It orchestrates across multiple different views
- It contains test-specific assertions or conditions

## Rules

- **No raw locators in tests.** Tests call view methods and view element fields only. If you need a new element, add it to the view first.
- **No `Thread.Sleep`.** Use `Wait()` from BaseTest for brief animation pauses. Use `WaitFor()` / `WaitForGone()` for element-based waits.
- **No `Console.WriteLine`.** Use `Reporter.Log()` for all logging.
- **Tests must be independent.** Each test can run in any order. `[SetUp]` presses Escape to reset state. If a test opens a panel, it must close it before ending.
- **No shared mutable state between tests.** Don't use class-level fields to pass data between `[Test]` methods. Each test sets up its own preconditions.
- **Clean up after yourself.** If a test opens a panel, close it. The next test expects a clean HUD.
- **Use `[TestCase]` for parameterized tests** when the same flow applies to multiple inputs:
  ```csharp
  [TestCase(AltKeyCode.X, "Events")]
  [TestCase(AltKeyCode.Z, "Places")]
  public void TestOpenPanelWithShortcut(AltKeyCode key, string panelName)
  ```

