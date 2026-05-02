# Visual Regression Snapshots

A pixel-by-pixel snapshot/baseline utility on top of the existing AltTester suite. Tests capture the current Explorer frame, compare it against a committed baseline PNG, and on mismatch attach a red-overlay diff to the Allure report.

Source: `Tests/Common/Snapshots/`. Sample test: `Tests/Tests/Visual/ExampleCubeFixture.cs`.

## Quick start — write a visual test

1. Reach the screen you want to snapshot. Wait for it to settle (`view.WaitFor()` and an optional `Wait(1)` for animation).
2. Call `Snapshot.AssertMatchesBaseline(name, tolerance)`.
3. Bootstrap the baseline once with `SNAPSHOT_MODE=record`. Commit the resulting PNG.
4. Re-run without the env var — the test now compares against the baseline and fails if the frame drifts beyond tolerance.

Minimal example, mirrors the project's existing test conventions:

```csharp
namespace ExplorerAutomation.Tests.Tests;

[TestFixture]
[AllureNUnit]
[AllureSuite("Visual Regression Tests")]
public class MyFeatureSnapshotTests : BaseTest
{
    [Test]
    public void TestSettingsPanelLooksRight()
    {
        Views.MainMenu.EventsButton.Click();
        Views.ExplorePanel.WaitFor();

        Views.ExplorePanel.SettingsTabButton.Click();
        Views.ExplorePanel.Settings.WaitFor();
        Wait(1); // let panel-open animation settle

        Snapshot.AssertMatchesBaseline("settings_panel", tolerance: 1.0);

        Views.ExplorePanel.CloseButton.Click();
        Views.ExplorePanel.WaitForGone();
    }
}
```

Bootstrap the baseline:

```bash
SNAPSHOT_MODE=record dotnet test --filter TestSettingsPanelLooksRight
# or via metaforge — the env var rides through:
SNAPSHOT_MODE=record metaforge explorer test --filter TestSettingsPanelLooksRight
```

This drops `Tests/Baselines/MyFeatureSnapshotTests/TestSettingsPanelLooksRight__settings_panel.png`. Commit it. Subsequent runs without the env var compare against it.

## Public API

```csharp
namespace ExplorerAutomation.Tests.Common.Snapshots;

public static class Snapshot
{
    // Capture the current frame, compare against the baseline, attach to Allure.
    // Throws Assert.Fail on mismatch or missing baseline (in Compare mode).
    public static void AssertMatchesBaseline(
        string name = "default",        // file name suffix; combined with TestClass + TestMethod
        double tolerance = 0.5,         // % of pixels allowed to differ
        SnapshotMode? mode = null,      // null = resolve from SNAPSHOT_MODE env var
        SKRect? clip = null,            // optional crop in screen coords
        SnapshotOptions options = null);

    // Capture-only — returns PNG bytes for ad-hoc use (no comparison).
    public static byte[] Capture(SKRect? clip = null, SnapshotOptions options = null);
}

public sealed record SnapshotOptions
{
    public int PerChannelTolerance { get; init; } = 8;     // 0-255; absorbs JPG noise
    public SKSizeI? ForceSize { get; init; } = null;       // opt-in resize before diff
    public int CaptureQuality { get; init; } = 100;        // forwarded to AltDriver
}

public enum SnapshotMode { Compare, Record, MissingOnly }

// Reusable pixel-diff utility (same SkiaSharp loop Snapshot uses).
public static class ImageDiff
{
    public static ImageDiffResult Compare(
        SKBitmap baseline, SKBitmap actual,
        int perChannelTolerance, double maxDifferingPixelPercent);
}
```

`Snapshot` is reachable from any test without extra usings — `SkiaSharp` and `ExplorerAutomation.Tests.Common.Snapshots` are global usings.

## Modes

Resolution order: per-call `mode:` argument → `SNAPSHOT_MODE` env var → default `Compare`.

| Mode | When to use | How to trigger |
|---|---|---|
| `Compare` (default) | CI / regular runs | env var unset |
| `Record` | Refresh after an intentional UI change — overwrites baseline, passes | `SNAPSHOT_MODE=record dotnet test --filter ...` |
| `MissingOnly` | Bootstrap a newly added snapshot — saves baseline if missing, compares otherwise | `SNAPSHOT_MODE=missingonly dotnet test --filter ...` |

Per-call override:

```csharp
Snapshot.AssertMatchesBaseline("settings_panel", mode: SnapshotMode.Record);
```

`SNAPSHOT_MODE` propagates through `metaforge explorer test ...` automatically — CliWrap inherits the parent process's environment.

**Missing-baseline behavior in Compare mode is strict**: the test fails with a message pointing at `SNAPSHOT_MODE=record`. CI catches forgotten-to-commit baselines instead of silently auto-recording them.

## Tolerance

One user-facing knob: `tolerance` = percentage of pixels allowed to differ (default `0.5`).

A pixel "differs" when any of its R/G/B channels deviates by more than `PerChannelTolerance` (default `8`/255). That floor exists because AltTester returns JPG bytes and JPG always introduces ±2-4 channel noise even at quality 100; without it every snapshot would trivially fail.

Tuning rule of thumb:

- Start with `tolerance: 1.0` for new snapshots.
- Tighten to `0.5` or below once the test is stably green.
- If CI vs. dev hosts disagree on font hinting / AA, raise `PerChannelTolerance` via `SnapshotOptions` to `12`.

## Baselines

Stored under `Tests/Baselines/<TestClass>/<TestMethod>__<name>.png`. Committed to git so reviewers can see UI changes in PR diffs.

`Tests.csproj` excludes them from the build output (`CopyToOutputDirectory="Never"`) so `bin/` stays small.

The default `name` is `"default"`. Use distinct names when one test method takes multiple snapshots, or when the test is parameterized.

## Allure attachments

Each snapshot wraps its work in an `AllureApi.Step("Snapshot: <name>")`. Attachments per outcome:

| Outcome | Attachments |
|---|---|
| Match | `<name>.actual.png` |
| Mismatch | `<name>.actual.png`, `<name>.baseline.png`, `<name>.diff.png` |
| Recorded / bootstrapped | `<name>.recorded.png` |
| Missing baseline (Compare mode) | `<name>.actual.png` (then `Assert.Fail`) |

The diff PNG paints mismatched pixels solid red over a 50%-faded copy of the actual frame so differences pop against the original context.

Allure writes attachments to `Tests/bin/Debug/net10.0/allure-results/<uuid>-attachment` (no extension). Add `.png` to view individually, or run `allure serve Tests/bin/Debug/net10.0/allure-results` for the full report UI. `metaforge explorer test` does this for you.

## Capture and image library

Captures via `AltDriver.GetScreenshot(size, screenShotQuality)` — JPG bytes returned over the wire — then decodes and re-encodes to PNG client-side. Avoids `AltDriver.GetPNGScreenshot(path)`, which has a known StackOverflow on the .NET driver in 2.3.x.

`Reporter.TakeScreenshot` (the existing TearDown failure-screenshot path) routes through the same safe capture helper.

Image library: **SkiaSharp 3.119** + native asset packages for macOS and Linux. MIT, self-contained. ImageSharp is excluded because Six Labors moved it to a paid commercial license at v3 for orgs over the $1M annual revenue threshold.

## Caveats and troubleshooting

**Size mismatch** — record and compare hosts run at different resolutions. Pin the runner resolution, or pass `SnapshotOptions { ForceSize = new SKSizeI(W, H) }` for a forced pre-diff resize (masks resolution-change bugs, so use sparingly).

**Frame is non-deterministic** — animations, day/night cycle, the world background behind transparent panels, blinking cursors, looping video tiles. Mitigations:

- Pass a `clip:` rect to crop to the stable region of the screen.
- Wait longer with `Wait(seconds)` before snapshotting so animations settle.
- Raise `tolerance` until V2 ships Unity-side animation freeze.
- Avoid full-screen snapshots of in-world scenes; target panels/menus.

**JPG noise floor** — every snapshot has ±2-4 channel noise from AltTester's JPG encoder. The default `PerChannelTolerance = 8` absorbs it. If you see flake on identical frames, raise to 12; if you see real changes being missed, lower to 4.

**Test fails immediately with `Snapshot baseline missing`** — expected on the first run. Re-run with `SNAPSHOT_MODE=record` to bootstrap, then commit the new PNG.

**Driver disconnected mid-snapshot** — not a snapshot bug; the AltTester WebSocket dropped. Confirm AltTester Desktop still shows the connected app, then re-run.

## File layout

```
Tests/Common/Snapshots/
  Snapshot.cs            # public API
  SnapshotMode.cs        # enum
  SnapshotOptions.cs     # advanced knobs
  ImageDiff.cs           # pure pixel-diff utility
  ImageDiffResult.cs     # diff result record
  ScreenshotCapture.cs   # AltDriver wrapper + SkiaSharp encode/crop helpers
  BaselineStore.cs       # baseline path resolution + I/O

Tests/Baselines/<TestClass>/<TestMethod>__<name>.png   # committed baselines
Tests/Tests/Visual/ExampleCubeFixture.cs               # sample test
```

## V2 — future Unity-side improvements

Out of scope for V1 but tracked for follow-up. In rough priority order:

1. Per-camera capture — biggest single determinism win; sidesteps world-background non-determinism for UI snapshots.
2. PNG-direct capture — Unity returns PNG bytes, eliminating the JPG noise floor entirely.
3. Animation freeze — Unity-side `Time.timeScale = 0` + animator suspend during snapshot.
4. Deterministic capture resolution — Unity-side switch to a fixed render target for snapshots.
5. AltObject-bounded crop — Unity returns an image cropped to a `RectTransform`'s screen rect.
6. Virtual input control — synthesized key/mouse/scroll events bypassing the OS input stack for reproducible CI runs.
7. Frame-stable wait primitive — wait until N frames have a pixel delta below epsilon.
8. World-time pin — fix in-world time-of-day for reproducible sky/lighting.
