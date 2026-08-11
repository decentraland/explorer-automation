# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working in the **C#/Unity** test stack under `explorer/`.

## Project Overview

UI automation tests for the Decentraland Explorer desktop client using AltTester SDK 2.3.0, NUnit 4, and Allure reporting. Standalone .NET 10.0 (C# 14) test project (not inside the Unity project). Also includes a `MetaForge.TestLogger` custom test logger project so that MetaForge can analyze test progress.

## Build & Test Commands

All paths below are relative to the repo root unless noted otherwise.

```bash
# Build
dotnet build explorer/Tests/

# Run all tests (requires AltTester Desktop running + instrumented Explorer connected)
dotnet test explorer/Tests/ --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test explorer/Tests/ --filter "ExplorePanelTests"

# Run a single test
dotnet test explorer/Tests/ --filter "TestOpenEventsFromSidebar"

# Automated workflow via MetaForge
metaforge explorer test <PR-number-or-branch>
```

Tests connect to AltTester Desktop at `127.0.0.1:13000`. The Explorer must be instrumented and connected before running.

## CI Scope

The InWorld PR workflow picks which fixtures to run by Roslyn reachability, so the size of a
run follows what the branch touches. A shared primitive — `Tests/Views/Elements/*`,
`BaseView`, `BaseTest` — is reachable from every fixture, so changing one expands the run
from a handful of fixtures to the whole suite, roughly 8 minutes to 25.

When a primitive change is incidental to the work (a suppressed screenshot, a corrected
default), land it on `main` first and rebase the branch onto it. The change leaves the PR
diff, the scope drops back to the fixtures actually under test, and the primitive still
ships. Keep it in the PR when the primitive *is* the thing under test — then the wide run is
the point.

## Architecture

**Page Object Model (POM)** pattern with NUnit test fixtures. Two main areas:

- **Views** (`explorer/Tests/Views/`) — Page objects wrapping AltTester locators (`Locatable`, `Clickable`, `Writable` in `explorer/Tests/Common/`). See the `view-writer` skill for detailed view conventions.
- **Tests** (`explorer/Tests/Tests/`) — NUnit test fixtures inheriting `BaseTest`, accessing views via `Views` property (`ViewContainer.Instance`).

### Test lifecycle

- `GlobalSetup` — runs once: connects `AltDriver`, initializes `ViewContainer`, sets up Unity log listener.
- `BaseTest` — `OneTimeSetUp` runs `EnsureInWorld()` (handles splash → auth → loading). `SetUp` presses Escape. `TearDown` screenshots on failure.

## Coding Conventions

- **C# style**: Use `var` when able. Private fields start with `_`, constants are `ALL_CAPS`. Use primary constructors.
- **Global usings** are in `GlobalUsings.cs` — don't add per-file usings for things already there.
- **Reporting**: Use `Reporter.Log()` (not `Console.WriteLine`). Use `Reporter.TakeScreenshot()` for manual screenshots.

## Interaction Mechanics

**Never drive a flow with a double-click.** Where the client offers a button for the same
action, press the button. Backpack equip is the worked example: `BackpackItemView` maps
`clickCount == 2` to Equip, but its Equip button raises the same `OnEquip`, so the button
gets there without depending on Unity's click counting.

Evidence: across the recorded CI runs the double-click equipped roughly half the time, and
every failure showed the presses arriving as single clicks — the item selected, nothing
equipped. `AltObject.Click` queues the pointer move and the first press in the same frame,
and the arriving pointer re-runs the client's hover animation, so consecutive presses can
resolve against different hierarchies and Unity restarts the count. Press count, interval,
parking the cursor first and settling before the press each moved the rate; none fixed it.
Switching to the button took the suite from one-or-both equip tests failing every run to
green.

**Tap is dead weight in this build.** AltTester's tap path compiles out its
pointerDown/Up/Click dispatch when the EventSystem runs `InputSystemUIInputModule`, which
this client does, leaving only `SendMessage` that uGUI `Button` never handles. A tap on a
uGUI control does nothing at all — reach for `Click`.

**A press must not share a frame with the pointer arriving.** Anything that appears on hover
(overlay buttons) needs the cursor parked with `AltDriver.MoveMouse` and the animation given
time before the click, or the press raycasts past a zero-scaled element.

## Skills

- **`view-writer`** — Always invoke this skill when creating new view classes, modifying existing views, adding elements/sections/sub-views, or registering views in `ViewContainer`. It contains the full POM conventions, region layout rules, and the workflow for discovering element locators via the `alttester-explorer` agent.
- **`test-writer`** — Always invoke this skill when creating new test classes, adding test methods, or modifying test logic. It contains the full test conventions, BaseTest lifecycle, interaction patterns, and rules for when to invoke `view-writer`.

Skills live at the repo root under `.claude/skills/` and apply only to work inside `explorer/`.
