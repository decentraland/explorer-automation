# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UI automation tests for the Decentraland Explorer client using AltTester SDK 2.3.0, NUnit 4, and Allure reporting. Standalone .NET 10.0 (C# 14) test project (not inside the Unity project). Also includes a `MetaForge.TestLogger` custom test logger project so that MetaForge can analyze test progress.

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

**Page Object Model (POM)** pattern with NUnit test fixtures. Two main areas:

- **Views** (`Tests/Views/`) — Page objects wrapping AltTester locators (`Locatable`, `Clickable`, `Writable` in `Tests/Common/`). See the `view-writer` skill for detailed view conventions.
- **Tests** (`Tests/Tests/`) — NUnit test fixtures inheriting `BaseTest`, accessing views via `Views` property (`ViewContainer.Instance`).

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
