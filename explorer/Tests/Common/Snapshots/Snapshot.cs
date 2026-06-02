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

    // Record-mode skip threshold: if the freshly captured frame differs from the existing baseline
    // by less than this percentage of pixels, the on-disk PNG is left untouched. Keeps `Record`
    // runs from churning visually-identical files (and the surrounding commit/PR diff).
    private const double RECORD_SKIP_TOLERANCE_PERCENT = 0.5;

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

            // Record mode: skip the write if the new frame is within tolerance of the existing
            // baseline (avoids churn), otherwise overwrite. Always attaches, always passes.
            if (resolvedMode == SnapshotMode.Record)
            {
                if (BaselineStore.Exists(baselinePath)
                    && TryDiscardUnchangedRecording(baselinePath, actualBmp, actualPng, name, options))
                    return;

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

    private static bool TryDiscardUnchangedRecording(
        string baselinePath,
        SKBitmap actualBmp,
        byte[] actualPng,
        string name,
        SnapshotOptions options)
    {
        SKBitmap existingBmp;
        try
        {
            var existingPng = BaselineStore.Read(baselinePath);
            existingBmp = SKBitmap.Decode(existingPng);
        }
        catch (Exception ex)
        {
            Reporter.Log($"Snapshot record: failed to read existing baseline for diff check ({ex.Message}). Overwriting.");
            return false;
        }

        if (existingBmp == null) return false;

        using (existingBmp)
        {
            // Resolution change is itself a meaningful update — let the overwrite happen.
            if (existingBmp.Width != actualBmp.Width || existingBmp.Height != actualBmp.Height)
                return false;

            var diff = ImageDiff.Compare(
                baseline: existingBmp,
                actual: actualBmp,
                perChannelTolerance: options.PerChannelTolerance,
                maxDifferingPixelPercent: RECORD_SKIP_TOLERANCE_PERCENT);

            using (diff.DiffBitmap)
            {
                if (!diff.Success) return false;

                Reporter.AttachPng($"{name}.actual", actualPng);
                Reporter.Log(
                    $"Snapshot record skipped (within tolerance): {name} " +
                    $"({diff.MismatchPercent:F3}% <= {RECORD_SKIP_TOLERANCE_PERCENT:F3}%, " +
                    $"{diff.DifferingPixels}/{diff.TotalPixels} pixels). Baseline left unchanged.");
                return true;
            }
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
