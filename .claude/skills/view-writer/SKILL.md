---
name: view-writer
description: Creates and modifies Page Object Model view classes (Tests/Views/) for the Decentraland Explorer UI automation test suite. Trigger this skill whenever the user wants to create a new view for any UI screen, panel, dialog, or overlay; add a section or tab to an existing panel view; add, remove, or modify Clickable, Writable, or Locatable element fields in a view; add helper methods to views; create nested sub-view classes like grid items or slots; register views in ViewContainer; or choose locator strategies (By.ID, By.NAME, By.PATH). Even if the user doesn't say "view" explicitly, trigger when they describe a UI screen with buttons, inputs, tabs, or interactive elements they want to automate. Do NOT trigger for writing test classes (Tests/Tests/), debugging test failures, modifying Common/ primitives (the Locatable/Clickable/Writable records themselves), or test lifecycle changes (GlobalSetup, BaseTest).
---

# View Writer

Write and modify Page Object Model view classes for the Decentraland Explorer automation tests (AltTester SDK, NUnit, C# 14).

## Architecture

Views wrap AltTester element locators into a typed hierarchy:

**Interaction primitives** (`Tests/Common/`):
- `Locatable(By, string)` — find, wait, check presence
- `Clickable` extends `Locatable` — adds click
- `Writable` extends `Clickable` — adds text input/output

**View base classes** (`Tests/Views/`):
- `BaseView(Locatable root)` — abstract base; delegates `WaitFor()`, `WaitForGone()`, `IsPresent()` to root
- `BaseClickableView(Clickable root)` — adds `Click()`; for small interactive elements that double as views (e.g. emote slots, grid items)
- `BaseSection(Locatable sectionLocator)` — for panel tabs/sections; lives in `Tests/Views/<Panel>Sections/`

**ViewContainer** — singleton holding all top-level views as read-only auto-properties. Tests access views via `Views` property (e.g. `Views.ExplorePanel.Events.WaitFor()`).

## Sourcing Locators

Each element needs a strategy (`By`) and a value.

**Strategy preference** (most stable first):
1. `By.ID` — Unity-assigned UUID. Survives renames and hierarchy changes. Always prefer this.
2. `By.NAME` — GameObject name. Good when IDs aren't available.
3. `By.PATH` — XPath-like path through the hierarchy. For repeated/dynamic elements.
4. `By.TAG` / `By.LAYER` / `By.COMPONENT` / `By.TEXT` — rarely appropriate.

**How to get locators:**
- If the user provides locator values directly, use those.
- Otherwise, use the `alttester-explorer` agent to discover locators from the running Explorer. This is the preferred approach. Follow this workflow:
  1. **Ask the user to confirm** that AltTester Desktop and the instrumented Explorer are both running, and that **the target UI panel/view is currently visible** on screen.
  2. **Wait for user confirmation** before proceeding.
  3. **Spawn the `alttester-explorer` agent** with a prompt describing which panel/view to inspect and what elements you need. The agent will query the AltTester MCP server and return structured locator data (IDs, names, paths, and suggested field types).
  4. **Use the returned locators** to write the view, preferring `By.ID` when available.
- If locator discovery fails or infrastructure isn't available, use `"TODO"` as the value and note which locators need filling in.

## Choosing the Right Base Class

| Scenario | Base class | File location |
|---|---|---|
| Full screen, dialog, or standalone panel | `BaseView` | `Tests/Views/{Screen}View.cs` |
| Tab or section within an existing panel | `BaseSection` | `Tests/Views/{Panel}Sections/{Panel}{Tab}View.cs` |
| Small clickable element that's also a view (e.g. slot, grid item) | `BaseClickableView` | Nested class inside parent view |

## Region Organization

Views use up to 5 `#region` blocks in this exact order. Only include regions that have content — omit empty ones:

1. **Elements** — `public readonly Clickable`/`Writable`/`Locatable` fields and constants (like array sizes)
2. **Setup** — Constructor body and private initialization helpers. Only needed when the primary constructor isn't enough (e.g. building arrays in a loop)
3. **Views** — Sub-view properties only (`{ get; } = new()`)
4. **Helper methods** — Public methods with `[AllureStep]` that compose element interactions, plus any private helpers they need
5. **Sub views** — Nested view class definitions. Always the last region in a class.

The Views region holds only property declarations. The actual nested class implementations go in the Sub views region at the end. This keeps the property list readable and the class implementations grouped together.

## Writing a View

### Minimal view (no interactive elements)

For screens that only need presence detection:

```csharp
/// <summary>
/// View for the loading screen displayed while the world is being loaded.
/// </summary>
public class LoadingScreenView() : BaseView(new(By.ID, "21e9d696-d866-4717-85c0-2b6e4f1c4d9d"));
```

One-liner with a primary constructor. XML doc comment describes the UI area.

### View with elements

```csharp
/// <summary>
/// View for the authentication screen where the user can log in or switch accounts.
/// </summary>
public class AuthenticationMainScreenView() :
    BaseView(new(By.NAME, "Authentication.MainScreen(Clone)"))
{
    #region Elements

    public readonly Clickable JumpIntoWorldButton       = new(By.ID, "646623d5-...");
    public readonly Clickable UseADifferentAccountButton = new(By.ID, "f658ab9f-...");

    #endregion
}
```

Fields are `public readonly` with the correct primitive type:
- `Locatable` — element you only need to wait for or check existence
- `Clickable` — element you need to click
- `Writable` — element you need to type into or read text from

### Panel view with section sub-views

When a panel has tabs, each tab becomes a section view:

```csharp
using ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// View for the explore panel containing navigation tabs and section content.
/// </summary>
public class ExplorePanelView() : BaseView(new(By.ID, "d5383a2a-..."))
{
    #region Elements

    public readonly Clickable CloseButton     = new(By.ID, "f507113e-...");
    public readonly Clickable EventsTabButton = new(By.ID, "8b6ee3fb-...");
    public readonly Clickable PlacesTabButton = new(By.ID, "261fa576-...");

    #endregion

    #region Views

    public ExplorePanelEventsView Events { get; } = new();
    public ExplorePanelPlacesView Places { get; } = new();

    #endregion
}
```

Sub-views are read-only auto-properties (`{ get; } = new()`), not fields. They're instantiated eagerly when the parent is created.

### Simple section

```csharp
namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Events tab within the explore panel.
/// </summary>
public class ExplorePanelEventsView() : BaseSection(new(By.ID, "f1208840-..."));
```

Sections inherit `BaseSection`, live in `Tests/Views/{Panel}Sections/`, and declare their own namespace.

### Complex section with nested views and helpers

When a section has repeated UI elements (slots, grid items), use arrays and nested view classes:

```csharp
namespace ExplorerAutomation.Tests.Views.ExplorePanelSections;

/// <summary>
/// Section view for the Backpack tab with wearable/emote sub-tabs and search.
/// </summary>
public class ExplorePanelBackpackView() : BaseSection(new(By.ID, "1666be29-..."))
{
    #region Elements

    public readonly Clickable WearablesTabButton = new(By.PATH, "//TabSelector/Avatar");
    public readonly Writable  SearchBar          = new(By.PATH, "//BackpackSection//SearchBar");

    #endregion

    #region Views

    public EmotesTab Emotes { get; } = new();

    #endregion

    #region Helper methods

    [AllureStep("Switch to emotes tab")]
    public void SwitchToEmotes()
    {
        EmotesTabButton.Click();
        Reporter.Log("Switched to emotes tab");
    }

    #endregion

    #region Sub views

    /// <summary>
    /// Sub-view for the emotes tab containing equippable emote slots.
    /// </summary>
    public class EmotesTab : BaseView
    {
        #region Elements

        private const int SLOT_COUNT = 10;
        public EmoteSlot[] Slots { get; }

        #endregion

        #region Setup

        public EmotesTab() : base(new(By.ID, "TODO"))
        {
            Slots = new EmoteSlot[SLOT_COUNT];
            for (var i = 0; i < SLOT_COUNT; i++)
                Slots[i] = new EmoteSlot(
                    new(By.PATH, $"//EmoteSlotContainer ({i})"),
                    new(By.PATH, $"//EmoteSlotContainer ({i})//Unequip"));
        }

        #endregion

        #region Helper methods

        [AllureStep("Unequip all emote slots")]
        public void UnequipAll()
        {
            for (var i = 0; i < SLOT_COUNT; i++)
            {
                Slots[i].Click();
                if (Slots[i].UnequipButton.IsPresent())
                {
                    Slots[i].UnequipButton.Click();
                    Reporter.Log($"Unequipped slot {i}");
                }
            }
            Reporter.Log("All emote slots unequipped");
        }

        #endregion

        #region Sub views

        /// <summary>
        /// A single emote slot with an unequip button.
        /// </summary>
        public class EmoteSlot(Clickable locator, Clickable unequipLocator)
            : BaseClickableView(locator)
        {
            #region Elements

            public Clickable UnequipButton { get; } = unequipLocator;

            #endregion
        }

        #endregion
    }

    #endregion
}
```

Key patterns:
- **Sub views region** at the very end holds all nested class definitions.
- **Views region** only has the `Emotes` property — clean and scannable.
- **Nested classes themselves** can also have their own Sub views region if they nest further.
- **Setup region** with an explicit constructor for array initialization in loops.
- **Primary constructor parameters** on nested views to pass locators from the parent.
- **Constants** (`SLOT_COUNT`) for array sizes, named `ALL_CAPS`.
- **Helper methods** always have `[AllureStep("...")]` and log via `Reporter.Log()`.

## Modifying Existing Views

### Adding elements

Add `public readonly` fields in the `#region Elements` block. Match the existing alignment style if the view pads field assignments with spaces.

### Adding sub-views

Add `{ get; } = new()` properties in `#region Views`. Put the nested class definition in `#region Sub views` at the end. If either region doesn't exist yet, create it in the correct position.

### Adding helper methods

Add methods in `#region Helper methods` with `[AllureStep("...")]` and `Reporter.Log()`. Helpers should compose element interactions into higher-level operations that tests would otherwise repeat.

### Adding a new section to an existing panel

1. Create the section class in `Tests/Views/{Panel}Sections/` inheriting `BaseSection`
2. Add a using for the sections namespace in the panel view if not already present
3. Add the section as a `{ get; } = new()` property in the panel view's Views region
4. Add the tab button as a `Clickable` in the panel's Elements region if not already there

## Registration

Every new view must be registered so tests can access it:

- **Top-level views**: Always add as `{ get; } = new()` property in `ViewContainer.cs`. This is required — without it, tests cannot access the view.
- **Sections**: Add as property in the parent panel view's Views region. Do NOT add to ViewContainer.
- **Nested views**: Instantiated by their parent, no registration needed.

## Naming

| Element | Convention | Example |
|---|---|---|
| View class | `{Screen}View` | `MainMenuView` |
| Section class | `{Panel}{Tab}View` | `ExplorePanelEventsView` |
| Nested view class | Descriptive noun | `EmoteSlot`, `EmoteGridItem` |
| Interaction field | `{Element}{Type}` | `CloseButton`, `SearchBar` |
| Helper method | `{Action}{Subject}` | `UnequipAll`, `SetEmote` |
| Constant | `ALL_CAPS` | `SLOT_COUNT` |

## Namespaces

- `ExplorerAutomation.Tests.Views` — top-level views, BaseView, BaseClickableView, ViewContainer
- `ExplorerAutomation.Tests.Views.<Panel>Sections` — section views and BaseSection

Don't add per-file usings for namespaces already in `GlobalUsings.cs` (NUnit, Allure, AltTester, Common, Views). Only add usings for section namespaces when a panel view references them.

## C# Style

- Use `var` when the type is obvious from context
- Private fields start with `_`, constants are `ALL_CAPS`
- Use primary constructors for views wherever possible
- XML doc comments on every view class describing which UI area it represents
