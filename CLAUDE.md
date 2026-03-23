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

- `BaseView` (abstract) — reusable interaction methods (`ClickObject`, `WaitForObject`, `WaitForObjectNotBePresent`, `IsObjectPresent`, `SetText`, `GetText`) with built-in timeout handling and Allure step tracking
  - `AuthenticationMainScreenView`, `SplashScreenView`, `LoadingScreenView`, `MainMenuView`
  - `ExplorePanelView` — contains section instances (`.Events`, `.Places`, `.Communities`, `.Navmap`, `.Backpack`, `.Gallery`, `.Settings`)
- `BaseSection` (extends `BaseView`) — adds section locator + `IsSectionVisible()`/`WaitForSectionVisible()`. Lives in `Tests/Views/ExplorePanelSections/`.

### Test lifecycle (BaseTest)

1. **OneTimeSetUp** — Connects `AltDriver`, creates all view objects via `InitializeViews()`, runs `EnsureInWorld()` (handles splash → auth → loading)
2. **SetUp** — Presses Escape to clear open panels
3. **TearDown** — Takes screenshot on failure
4. **OneTimeTearDown** — Stops driver, attaches Unity logs to Allure

All test classes inherit `BaseTest` and use its pre-initialized view properties directly — never create view instances in tests.

## Coding Conventions

- **Locators**: `private readonly (By, string)` tuples with `_` prefix (e.g., `_closeButtonLocator`). Prefer `By.ID` (UUID-based).
- **C# style**: Use `var` when able. Fields start with `_`, constants are `ALL_CAPS`.
- **Waits**: Use `BaseView` wait methods. Never use `Thread.Sleep` directly in tests. `Wait(seconds)` only for brief animation pauses (< 1s).
- **Reporting**: Use `Reporter.Log()` (not `Console.WriteLine`). Add `[AllureStep("description")]` to public view methods.
- **Tests**: Attributes `[TestFixture]`/`[AllureSuite]` on class, `[Test]`/`[AllureTest]` on methods. Tests must be independent.
- **Global usings** are in `GlobalUsings.cs` — don't add per-file usings for things already there.

### Naming

| Element | Convention | Example |
|---|---|---|
| Test class | `{Feature}Tests` | `ExplorePanelTests` |
| Test method | `Test{Action}{Subject}` | `TestOpenEventsFromSidebar` |
| View class | `{Screen}View` | `MainMenuView` |
| Section class | `{Name}Section` | `EventsSection` |
| Locator field | `_{element}Locator` | `_closeButtonLocator` |

### Namespaces

- `ExplorerAutomationTests.Tests`
- `ExplorerAutomationTests.Views`
- `ExplorerAutomationTests.Views.ExplorePanelSections` (and similar subfolders for other panels)

## Adding New Views/Tests

**New view**: Create in `Tests/Views/` (or `Tests/Views/<Panel>Sections/` for sections), inherit `BaseView`/`BaseSection`, add as property in `BaseTest`, initialize in `InitializeViews()`.

**New test**: Create in `Tests/Tests/`, inherit `BaseTest`, use pre-initialized view properties.
