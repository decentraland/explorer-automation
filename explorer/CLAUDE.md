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

Sharding follows the same rule. The Windows leg splits the resolved scope with
`explorer/ci/ScopeInWorldTests`, which bin-packs whole fixtures by counted tests — intra-fixture
`[Order]` is load-bearing — and plans a single shard on any doubt, or at 15 tests and under, where
a second runner's startup costs more than it saves. Never a literal fixture list: the two
hardcoded filters kept running the same 14 fixtures while the category grew, and the eight
Camera/Minimap tests ran nowhere — run 31591893759 discovered 66 tests on macOS against 31 + 27 on
Windows. A test filter that matches nothing exits clean, which is why each shard reconciles the
count MetaForge reports against its plan.

Callers say what to split, never how: the Windows leg plans every split itself, so a merge to main
and a dispatch shard the same way a PR does. The invariant and the paths that hold it up are in
[explorer/ci/CLAUDE.md](ci/CLAUDE.md).

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

**A press outside the viewport is dropped, and AltTester calls it a success.** `GraphicRaycaster`
returns before hit-testing anything once the pointer leaves the camera's 0..1 viewport, so nothing
is pressed and no backdrop swallows it either — the symptom is a timeout on whatever the press was
meant to produce, with the panel still open. Check the target's `y` against the screen height
before reaching for SoftMask or a pooled rebuild.

What decides this is the **aspect ratio, not the pixel size**. Every full-screen panel's canvas is
`ScaleWithScreenSize` against a 1920x1080 reference matched on width, so the layout gets
`1920 * height / width` units of vertical room: 1024x768 leaves it 1440 and works, 1920x800 leaves
it 800 and puts the passport and community-detail close buttons past the top edge. Keep the
viewport at or under 16:9. `Viewport.RequireUsable` logs the size every run and fails the fixture
below 900 units; `explorer/ci/Resolve-ExplorerRenderSize.ps1` is what keeps CI inside that.

**Tap is dead weight in this build.** AltTester's tap path compiles out its
pointerDown/Up/Click dispatch when the EventSystem runs `InputSystemUIInputModule`, which
this client does, leaving only `SendMessage` that uGUI `Button` never handles. A tap on a
uGUI control does nothing at all — reach for `Click`.

**A press must not share a frame with the pointer arriving.** Anything that appears on hover
(overlay buttons) needs the cursor parked with `AltDriver.MoveMouse` and the animation given
time before the click, or the press raycasts past a zero-scaled element.

**A control near the top of a panel gets covered by that panel's toast.** Panels raise a
`WarningNotificationView` across their header — the passport does it on every open, because emote
thumbnails fail to load on this chassis — and it blocks raycasts for the five seconds it is up.
A press it swallows does nothing at all, because a toast is not a close affordance, so the symptom
is a timeout with the panel still open rather than anything that names the toast. Whether the
press lands inside that window depends on how fast the panel's content loads, so the test passes
or fails at random. Dismiss it rather than waiting, and do it through the view's own
`Hide` — reaching into its `CanvasGroup` reimplements the client and rots the moment `Hide`
changes. See `PassportEditPress.ClearErrorNotification`. Suspect this for anything in the top strip
of a panel; controls further down are unaffected, which is why one of a pair of tests can fail
alone.

Calling a client method takes some care: `AltCallComponentMethodForObjectCommand` selects an
overload by **parameter count** and never fills in defaults, so a method whose arguments are all
optional still needs every one supplied. Leaving `typeOfParameters` empty makes it match on count
alone, and each argument is JSON-deserialized into the parameter's type — an empty JSON object is
the way to hand over a `default` struct such as a `CancellationToken`.

**`SoftMask` vetoes presses inside it.** `Coffee.UISoftMask.SoftMask` implements
`ICanvasRaycastFilter`, and Unity consults the filters on a hit graphic's *ancestors*, so a
SoftMask on a container rejects every press in its whole subtree — the press falls through to
whatever is behind. The passport carries three (`BackgroundContainer`, `Viewport`, one nested)
and `ProfileNameEditor` two, one of which gates its `SaveButton`; `ExplorePanelUI` carries none,
which is why that panel has always been drivable. Disabling the component makes its filter
permissive (`IsRaycastLocationValid` returns true when not `isActiveAndEnabled`), which is what
`PassportEditPress.DisableSoftMasks` does. Grep a prefab for `SoftMask` before assuming a
press-does-nothing symptom is timing.

**A panel whose backdrop is a close button hides its own failures.** The passport's
`Background_Close` is a full-screen close `Button` at sibling index 0, so a press that misses
anything closes the panel — which is indistinguishable from a press that hit `CloseButton`. Tests
that only read text and then close therefore pass whether or not their clicks land. Prefer a
control with an observable non-close outcome (a tab switching sections) when checking that presses
reach a panel at all, and be suspicious of "passing" coverage built on a close.

**When a press lands but nothing happens, check `interactable` before blaming the harness.**
uGUI raises no event for a disabled button and there is no error to read. The name editor gates
`saveButtonInteractable` on `NameInputFieldView.IsValidName` — at most 15 characters — while the
input accepts three times that, so an over-long name types in fine and Save silently does nothing.

**`FindObjectAtCoordinates` is not a hit test.** It answers which object *contains* a point in
scene-graph order, so for stacked UI it returns the first full-screen ancestor rather than what a
press would reach — it reported a passport backdrop for points over a working button. Do not use
it to reason about where a press lands.

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

A ceiling that is routinely 75-90% spent is a scheduled failure. `EmotesTab.WaitForGridPageLoaded`
measured 30.2s, 33.0s and 35.5s on runs that passed, against `CONTENT_TIMEOUT`'s 40s — and duly ran
out of it twice, once with nothing loaded and once with thirteen of sixteen tiles done. Nothing
about those runs differed except that the grid was slower, which is why it read as flaky for months.
Before believing that of a wait, read the step's duration on the runs that passed. Whole-page waits
now take `GRID_PAGE_TIMEOUT` instead: sixteen sequential streams do not belong on a single-element
ceiling. A wait shared across several elements also overshoots its own deadline, since the page
floors each tile at `Math.Max(remaining, 1D)` — blowing the budget costs it plus a second per
remaining tile, which is why the empty-grid failure reported 55s against a 40s ceiling.

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
`FindTopLeftVisibleCard` is the worked example, in two shapes: the events view scans day columns
left to right and stops at the first that answers, the places view scans the whole grid for the
smallest `mobileY` and breaks ties on `x`. Probe the element that will be clicked, not its parent.
The places grid also gates presses behind its skeleton — `WaitForResultsInteractive` waits for
`LoadedState`'s `CanvasGroup` to start blocking raycasts, which only happens once the fade ends.

The two backpack grids do not behave alike, and the same fix is right for one and wrong for the
other. In the **wearables** grid the unindexed `FirstLoadedGridItem` path answers with a tile that
is neither the one displayed first nor the same one after a re-bind — the pagination round trip
read "Blue Star Earring" leaving page 1 and "Blue T-Shirt" returning to it, and the final frame
showed the selection bottom-right while the visual first item was top-left. Picking the
left-top-most loaded tile by screen position fixes it. In the **emotes** grid the same path is
stable (two runs a day apart both read "Ho Ho Ho"), and picking by position instead *destabilises*
it: the leading tile intermittently reports not-loaded, so the pick alternates between two tiles in
one row (x=873 and x=1010, both mobileY=331) and the round trip fails. Measure before porting a
grid fix sideways.

Still index-addressed and carrying the same latent bug: `PlacesTests`'s search and filter
assertions, which assume index 0 is the leading result, and `CommunitiesTests` (`Cards[0].Title`).
Its detail flow brute-forces the problem by clicking cards 0-4 in turn. The
backpack pools are safe by another route — they blank and re-park tiles rather than scrolling
them away, and are already fenced by `LoadedIndicator` / `HasFullGridPage`.

## Environment Coupling

A test that asserts flag-gated UI is asserting the environment. A flag flip then fails it on every
run until the flag comes back — not flakiness, and not something waiting out or a rerun fixes.
`alfa-marketplace-credits` going off did exactly this to the navbar test.

**Ask the client, not the flag service.** `FeatureFlags` in `Tests/Common/` calls
`AltTesterFeatureFlagsProbe` in the running Explorer, so a test reads the value the UI actually
gated on. Deriving it here instead is not equivalent in either direction: the document's hostname
strategy is evaluated from the request's `referer`, and the client folds app arguments and editor
overrides on top of whatever the document says.

**Gate both directions.** Present when the flag is on, *absent* when it is off. An assertion that
only fires one way goes quiet on the inverse bug — UI that outlives its flag.

Two shapes of gate, not interchangeable:

- **Resolved feature** — `FeatureFlags.Feature("CameraReel")`, backed by `FeaturesRegistry`.
  Definitive both ways.
- **Flag plus a `wallets` allowlist** — `FeatureFlags.UserGated("alfa-communities")`. Off is
  definitive; on is only definitive while the allowlist is empty, since the run's wallet is not
  known here, so a non-empty one is reported rather than guessed. Marketplace Credits and
  Communities both resolve this way, and `FeatureId.Communities` is deliberately never registered
  — reading the registry for it answers false however the flag stands.

Which shape a feature uses is decided in `SidebarController.OnViewInstantiated` and the client's
`IsUserAllowedToUseTheFeatureAsync` helpers; read those before mapping a button to a gate.

A gate can also be unanswerable. The probe is client code, and each CI leg resolves its own newest
*reachable* dev build (`explorer/ci/Resolve-ExplorerBuildUrl.ps1` — dev because branch builds keep
the AltTester define that release artifacts strip). Builds publish per platform at different
times, so a leg's build can predate a probe that just landed, and the probe then answers
`componentNotFound` — an unanswerable question, not a verdict. `FeatureFlags` reads it as
`Expected.Unknown` and the run leaves flag-gated UI unasserted instead of failing it, with
`FeatureFlags.IsAvailable` separating that Unknown from the allowlist one in the log. Do not
hard-depend a test on a client capability newer than the builds the legs may resolve.

Fetching the document directly is still right when there is no client to ask — reading a CI failure
after the fact. `https://feature-flags.decentraland.{org|zone}/explorer.json` with a
`referer: https://decentraland.{env}` header; without it the response is a subset in which live
features look off. Keys carry the `explorer-` prefix the client strips, and a disabled flag is
*absent* rather than `false`. CI runs against `org`.

The Nearby Voice Chat tip appears once loading completes and closes only on its own two buttons —
Escape does not dismiss it. Its dismissal is a per-profile pref and CI creates a fresh account per
run, so it is up for the whole run and swallows clicks in the bottom-left.

That account lives in the client's state directory — on Windows
`%USERPROFILE%\AppData\LocalLow\Decentraland\Explorer` — where `userdata_*.json` is both the
client's prefs file and where `mf account login` parks the session it provisioned. Clear the two
logs there by name, never the directory: deleting userdata after login boots the client with
nothing to log in as, which failed all 33 tests of both Windows shards on run 31611147158 —
`EnsureInWorld` reported a cached-account screen and then no Jump Into Decentraland button to press. `mf account create` prints the seed phrase and auth token, so CI captures
its output rather than letting credentials reach the Actions log.

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

**The same aspect breaks `catch`.** It invokes the wrapped method through `MethodBase.Invoke`, so
whatever the method throws arrives at the caller inside a `TargetInvocationException` — one per
decorated frame crossed, which is why stacks show the chain repeated. `catch (AssertionException)`
around anything decorated reads correctly and never fires; walk `InnerException` instead. PR #70
shipped exactly that mistake, compiled clean and changed nothing, and only the CI stack showed it.
A fix whose whole job is to swallow an exception cannot be verified by a build.

The client's launcher parses its own arguments, and any token without a `--` prefix is taken as the
value of the last `--` key it saw. A single-dash Unity argument placed after one therefore
overwrites it: `-screen-fullscreen 0` trailing `--resolution 1749x984` left the client with
`resolution = 0`. Put single-dash arguments first, and read the client's own `Arg N: key = value`
dump near the top of Player.log to see what it actually parsed. Note also that `--resolution` is the
only lever that sets the viewport — `NativeWindowManager.Initialize` re-applies its own windowed
size at startup and overwrites whatever `-screen-width`/`-screen-height` asked for. Without
`--resolution` that size is the saved pref or `ResolutionUtils.GetDefaultResolution`, which keeps
only 16:9 and 16:10 modes whose smaller dimension exceeds 1024 and falls back to 1920x1080 when
nothing survives — how a 1024x768 desktop got asked for a window larger than itself. And
`--position` is the parcel to spawn at, not a window coordinate: 0,0 is Genesis Plaza, whose crowd
and scene load no UI test should sit in.

When checking what display a Windows host actually has, do not read
`[System.Windows.Forms.Screen]::Bounds`: PowerShell is not DPI-aware, so Bounds answers in scaled
pixels — a 2560x1600 display at 150% reads 1707x1067. `EnumDisplaySettings` reports the real mode,
which is what `explorer/ci/Resolve-ExplorerRenderSize.ps1` sizes the render from.

## Skips

A skip reason is a claim, and an old one has usually never been re-tested. Every macOS skip this
suite carried named a cause the client contradicts. `TestOpenPlaceDetail` blamed chassis timing,
while the driver log showed its press dispatched below the bottom of the screen. The two Gallery
tests blamed a TCC prompt for `~/Downloads`, which the Explorer only touches when a reel is
downloaded — that skip began as a pre-macOS `Assert.Ignore("can't access user device")` and was
given its explanation afterwards, so no run ever stood behind it.

Read the client for the mechanism before trusting a reason, and check whether another test already
drives the same state by a different route: `TestSwitchBetweenAllTabs` had been opening
`GallerySection` all along, on the very chassis where the Gallery tests were skipped for supposedly
not being able to.

## Skills

- **`view-writer`** — Always invoke this skill when creating new view classes, modifying existing views, adding elements/sections/sub-views, or registering views in `ViewContainer`. It contains the full POM conventions, region layout rules, and the workflow for discovering element locators via the `alttester-explorer` agent.
- **`test-writer`** — Always invoke this skill when creating new test classes, adding test methods, or modifying test logic. It contains the full test conventions, BaseTest lifecycle, interaction patterns, and rules for when to invoke `view-writer`.

Skills live at the repo root under `.claude/skills/` and apply only to work inside `explorer/`.
