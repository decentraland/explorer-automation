using SkiaSharp;

namespace ExplorerAutomation.Tests.Common.Snapshots;

/// <summary>
/// Visual-regression assertions: capture the current Explorer frame via AltTester and
/// compare it pixel-by-pixel against a committed baseline PNG under <c>Tests/Baselines/</c>.
///
/// Common false-positive sources (mitigations in parens):
///   - Resolution mismatch between record and run hosts (pin the runner resolution; use ForceSize as last resort).
///   - Day/night cycle in the world background (clip to the UI panel rect).
///   - Animations: hover effects, idle anims, blinking cursors, video tiles (clip them out, raise tolerance, or wait for V2 freeze).
///   - GPU/OS differences in font hinting / AA (absorbed by PerChannelTolerance default 8; raise to 12 if CI vs. dev disagree).
///   - Localization / fonts (lock locale or clip text regions).
///   - Avatar / world background behind transparent panels (default to clipped snapshots; full-screen for menus only).
///   - Mid-load captures (caller waits for the UI to settle before snapshotting, same as today's interaction tests).
/// </summary>
public static class Snapshot
{
    private const string ENV_VAR = "SNAPSHOT_MODE";

    public static void AssertMatchesBaseline(
        string name = "default",
        double tolerance = 0.5,
        SnapshotMode? mode = null,
        SKRect? clip = null,
        SnapshotOptions options = null)
    {
        options ??= new SnapshotOptions();
        var resolvedMode = ResolveMode(mode);
        var baselinePath = BaselineStore.ResolvePath(name);

        AllureApi.Step($"Snapshot: {name}", () =>
        {
            using var actualBmp = CaptureAndClip(clip, options);
            var actualPng = ScreenshotCapture.EncodePng(actualBmp);

            // Record mode: overwrite, attach, pass.
            if (resolvedMode == SnapshotMode.Record)
            {
                BaselineStore.Write(baselinePath, actualPng);
                Reporter.AttachPng($"{name}.recorded", actualPng);
                Reporter.Log($"Snapshot recorded: {baselinePath}");
                return;
            }

            // Missing baseline.
            if (!BaselineStore.Exists(baselinePath))
            {
                if (resolvedMode == SnapshotMode.MissingOnly)
                {
                    BaselineStore.Write(baselinePath, actualPng);
                    Reporter.AttachPng($"{name}.recorded", actualPng);
                    Reporter.Log($"Snapshot bootstrapped (missing baseline): {baselinePath}");
                    return;
                }

                Reporter.AttachPng($"{name}.actual", actualPng);
                Assert.Fail(
                    $"Snapshot baseline missing: {Path.GetRelativePath(Directory.GetCurrentDirectory(), baselinePath)}\n" +
                    $"Run with {ENV_VAR}=record to bootstrap, then commit the new PNG.");
                return;
            }

            // Compare against existing baseline.
            var baselinePng = BaselineStore.Read(baselinePath);
            using var baselineBmp = SKBitmap.Decode(baselinePng)
                ?? throw new InvalidOperationException(
                    $"Failed to decode baseline PNG at {baselinePath} ({baselinePng.Length} bytes).");

            ImageDiffResult result;
            try
            {
                result = ImageDiff.Compare(
                    baseline: baselineBmp,
                    actual: actualBmp,
                    perChannelTolerance: options.PerChannelTolerance,
                    maxDifferingPixelPercent: tolerance);
            }
            catch (ArgumentException ex)
            {
                Reporter.AttachPng($"{name}.actual", actualPng);
                Reporter.AttachPng($"{name}.baseline", baselinePng);
                Assert.Fail($"Snapshot size mismatch: {ex.Message}");
                return;
            }

            if (result.Success)
            {
                Reporter.AttachPng($"{name}.actual", actualPng);
                Reporter.Log(
                    $"Snapshot match: {name} ({result.MismatchPercent:F3}% < {tolerance:F3}%, " +
                    $"{result.DifferingPixels}/{result.TotalPixels} pixels)");
                result.DiffBitmap.Dispose();
                return;
            }

            using (result.DiffBitmap)
            {
                var diffPng = ScreenshotCapture.EncodePng(result.DiffBitmap);
                Reporter.AttachPng($"{name}.actual", actualPng);
                Reporter.AttachPng($"{name}.baseline", baselinePng);
                Reporter.AttachPng($"{name}.diff", diffPng);
                Assert.Fail(
                    $"Snapshot mismatch: {name} ({result.MismatchPercent:F3}% differing, " +
                    $"threshold {tolerance:F3}%, {result.DifferingPixels}/{result.TotalPixels} pixels)");
            }
        });
    }

    public static byte[] Capture(SKRect? clip = null, SnapshotOptions options = null)
    {
        options ??= new SnapshotOptions();
        using var bmp = CaptureAndClip(clip, options);
        return ScreenshotCapture.EncodePng(bmp);
    }

    private static SKBitmap CaptureAndClip(SKRect? clip, SnapshotOptions options)
    {
        var bmp = ScreenshotCapture.CaptureBitmap(options.CaptureQuality);
        if (clip == null) return bmp;

        try
        {
            return ScreenshotCapture.Crop(bmp, clip.Value);
        }
        finally
        {
            bmp.Dispose();
        }
    }

    private static SnapshotMode ResolveMode(SnapshotMode? perCallMode)
    {
        if (perCallMode.HasValue) return perCallMode.Value;
        var raw = Environment.GetEnvironmentVariable(ENV_VAR);
        if (string.IsNullOrWhiteSpace(raw)) return SnapshotMode.Compare;
        if (Enum.TryParse<SnapshotMode>(raw, ignoreCase: true, out var parsed)) return parsed;
        Reporter.Log($"Unknown {ENV_VAR}='{raw}'. Falling back to Compare.");
        return SnapshotMode.Compare;
    }
}
