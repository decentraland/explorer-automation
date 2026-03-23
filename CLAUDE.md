# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UI automation tests for the Decentraland Explorer client using AltTester SDK 2.3.0, NUnit 4, and Allure reporting. Standalone .NET 10.0 (C# 14) test project (not inside the Unity project).

## Build & Test Commands

```bash
# Build
dotnet build

# Run all tests (requires AltTester Desktop running + instrumented Explorer connected)
dotnet test Tests/ --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test Tests/ --filter "ExplorePanelTests"

# Run a single test
dotnet test Tests/ --filter "TestOpenEventsFromSidebar"

# Automated workflow via MetaForge
metaforge explorer test <PR-number-or-branch>
```

Tests connect to AltTester Desktop at `127.0.0.1:13000`. The Explorer must be instrumented and connected before running.

## Architecture

**Page Object Model (POM)** pattern with NUnit test fixtures.

### View hierarchy

- `BaseView` (abstract) — reusable interaction methods with built-in timeout handling and Allure step tracking:
  - `ClickObject(locator, timeout)` — Wait for object then click
  - `TapObject(locator, count, timeout)` — Multi-tap support
  - `WaitForObject(locator, timeout)` — Wait for object to appear
  - `WaitForObjectWhichContains(locator)` — Partial name match
  - `WaitForObjectNotBePresent(locator, timeout)` — Wait for disappearance
  - `IsObjectPresent(locator)` — Check presence without throwing
  - `FindObject(locator)` — Direct find (throws if not found)
  - `SetText(locator, text, timeout)` — Set input field text
  - `GetText(locator, timeout)` — Read text content
  - `AuthenticationMainScreenView`, `SplashScreenView`, `LoadingScreenView`, `MainMenuView`
  - `ExplorePanelView` — contains section instances (`.Events`, `.Places`, `.Communities`, `.Navmap`, `.Backpack`, `.Gallery`, `.Settings`)
- `BaseSection` (extends `BaseView`) — adds section locator + `IsSectionVisible()`/`WaitForSectionVisible()`. Lives in `Tests/Views/ExplorePanelSections/`. Other panels with sections follow the same pattern in their own subfolder (e.g., `Views/ChatPanelSections/`).

### Test lifecycle (BaseTest)

1. **OneTimeSetUp** — Connects `AltDriver`, creates all view objects via `InitializeViews()`, runs `EnsureInWorld()` (handles splash → auth → loading)
2. **SetUp** — Presses Escape to clear open panels
3. **TearDown** — Takes screenshot on failure
4. **OneTimeTearDown** — Stops driver, attaches Unity logs to Allure

All test classes inherit `BaseTest` and use its pre-initialized view properties directly — never create view instances in tests.

## Coding Conventions

- **Locators**: `private readonly (By, string)` tuples with `_` prefix (e.g., `_closeButtonLocator`). Strategy preference: `By.ID` (UUID, most stable) > `By.NAME` > `By.PATH` > `By.TAG`/`By.LAYER`/`By.COMPONENT`/`By.TEXT`.
- **C# style**: Use `var` when able. Fields start with `_`, constants are `ALL_CAPS`.
- **Waits**: Use `BaseView` wait methods. Never use `Thread.Sleep` directly in tests. `Wait(seconds)` only for brief animation pauses (< 1s).
- **Reporting**: Use `Reporter.Log()` (not `Console.WriteLine`). Use `Reporter.TakeScreenshot()` for manual screenshots at checkpoints. Add `[AllureStep("description")]` to public view methods.
- **Tests**: Attributes `[TestFixture]`/`[AllureSuite]` on class, `[Test]`/`[AllureTest]` on methods. Use `[TestCase]` for parameterized tests. Tests must be independent.
- **Global usings** are in `GlobalUsings.cs` — don't add per-file usings for things already there.

### Naming

| Element | Convention | Example |
|---|---|---|
| Test class | `{Feature}Tests` | `ExplorePanelTests` |
| Test method | `Test{Action}{Subject}` | `TestOpenEventsFromSidebar` |
| View class | `{Screen}View` | `MainMenuView` |
| Section class | `{Name}Section` | `EventsSection` |
| Locator field | `_{element}Locator` | `_closeButtonLocator` |
| Public view method | `{Action}{Subject}` | `WaitForPanelOpen` |

### Namespaces

- `ExplorerAutomation.Tests.Tests`
- `ExplorerAutomation.Tests.Views`
- `ExplorerAutomation.Tests.Views.ExplorePanelSections` (and similar subfolders for other panels)

## Adding New Views/Tests

**New view**: Create in `Tests/Views/` (or `Tests/Views/<Panel>Sections/` for sections), inherit `BaseView`/`BaseSection`, add as property in `BaseTest`, initialize in `InitializeViews()`.

**New test**: Create in `Tests/Tests/`, inherit `BaseTest`, use pre-initialized view properties.
