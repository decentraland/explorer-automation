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

## Waits and Retries

**Never spend wall-clock without a need behind it.** Adding a wait, widening a ceiling and
raising an attempt count are the same move, and each has to name the failure it prevents.
"To be safe" is not one, and neither is symmetry with a neighbouring call site.

Wait on the state a pause would stand in for — a flag clearing, a label naming what was just
selected, a tile leaving its loading state. A fixed pause is only right where there is nothing
to observe, or where the delay *is* the measurement (the interval between two settle samples).

Size a retry loop's per-attempt budget to what the retry fixes. A click the grid rebuild
swallowed is only undone by another click, so a short budget plus a fresh click beats polling
one dead selection for the default ceiling — and it lowers the worst case, not just the common
one. Long ceilings are only ever spent on a path that is already failing.

**A wait's position decides whether it can become a condition.** One that *follows* an
interaction is waiting for that interaction's effect, and the effect is observable. One that
*precedes* an interaction is guarding readiness, and readiness usually has no signal — so
converting it trades a real guarantee for a weaker one, because **findable is not ready**. A
panel-open helper whose test then reads a grid or a search result keeps its settle; the
presence-only shortcut tests do not need one.

Timeouts are floors, not ceilings. `WaitForObject` polls with a round trip per iteration, so a
15s probe spends about 20s. A probe you *expect* to fail therefore costs more than its number
says — poll `IsPresent` against a deadline instead of catching a `WaitFor`.

A verification shot costs ~200ms, so suppressing one is a timing change as much as a report
change. In a content-loading fixture, treat removing a shot the way you would treat removing a
wait.

Boot is a per-process cost, not a per-fixture one: the client stays in world between fixtures,
so `EnsureInWorld` confirms the main menu and returns rather than re-running the splash, auth
and loading-screen path. Anything that leaves the client elsewhere still gets the full path.

## Pooled Lists

The explore panel's grids and day columns are recycling lists, so a hierarchy index names a
*slot*, not a position. The slot may be bound to different data than the label you just read, or
parked outside the viewport — and a click on an off-screen item is dispatched off-screen and
silently does nothing, which reads in the report as a click that landed and achieved nothing.

Pick by screen position instead. The `AltObject` from `WaitFor` carries `y` (Unity's bottom-left
origin) and `mobileY` (the same point measured from the top), so `y > 0 && mobileY > 0` proves the
centre is on screen without asking for the screen size.
`ExplorePanelEventsView.FindTopLeftVisibleCard` is the worked example — scan columns left to
right, keep the smallest `mobileY`, stop at the first column that answers.

Still index-addressed and carrying the same latent bug: `PlacesTests` (the detail click, and the
search and filter assertions that assume index 0 is the leading result) and `CommunitiesTests`
(`Cards[0].Title`). Its detail flow brute-forces the problem by clicking cards 0-4 in turn. The
backpack pools are safe by another route — they blank and re-park tiles rather than scrolling
them away, and are already fenced by `LoadedIndicator` / `HasFullGridPage`.

## Environment Coupling

A test that asserts flag-gated UI is asserting the environment. Read the document the client
reads: `https://feature-flags.decentraland.{org|zone}/explorer.json` with a
`referer: https://decentraland.{env}` header. The hostname strategy is evaluated from that header,
so a bare request answers with a subset in which live features look off. Keys carry the
`explorer-` prefix the client strips, and a disabled flag is *absent* rather than `false`. CI
runs against `org`.

The Nearby Voice Chat tip appears once loading completes and closes only on its own two buttons —
Escape does not dismiss it. Its dismissal is a per-profile pref and CI creates a fresh account per
run, so it is up for the whole run and swallows clicks in the bottom-left.

## Diagnosing a Failed Run

Check whether the test took a different code path than the last green run before blaming the diff.
A branch that only runs when live data exists — or does not — can sit unexercised for months and
then fail on its first outing. That looks exactly like a regression and is not one.

The artifact carries the evidence. Every test attaches its final frame, and
`AltTester-Server.log` records each command with the object's resolved screen coordinates, which
is how a swallowed click is told apart from one dispatched outside the viewport.

An `[AllureStep]` method's arguments are JSON-serialized into the result file, and a delegate
argument drags its whole reflection graph in at ~14MB per call. Register a type formatter in
`GlobalSetup` for any new delegate-taking step.

## Skills

- **`view-writer`** — Always invoke this skill when creating new view classes, modifying existing views, adding elements/sections/sub-views, or registering views in `ViewContainer`. It contains the full POM conventions, region layout rules, and the workflow for discovering element locators via the `alttester-explorer` agent.
- **`test-writer`** — Always invoke this skill when creating new test classes, adding test methods, or modifying test logic. It contains the full test conventions, BaseTest lifecycle, interaction patterns, and rules for when to invoke `view-writer`.

Skills live at the repo root under `.claude/skills/` and apply only to work inside `explorer/`.
