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

## Skills

- **`view-writer`** — Always invoke this skill when creating new view classes, modifying existing views, adding elements/sections/sub-views, or registering views in `ViewContainer`. It contains the full POM conventions, region layout rules, and the workflow for discovering element locators via the `alttester-explorer` agent.
- **`test-writer`** — Always invoke this skill when creating new test classes, adding test methods, or modifying test logic. It contains the full test conventions, BaseTest lifecycle, interaction patterns, and rules for when to invoke `view-writer`.

Skills live at the repo root under `.claude/skills/` and apply only to work inside `explorer/`.
