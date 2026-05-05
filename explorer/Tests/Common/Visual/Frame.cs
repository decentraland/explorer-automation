using ExplorerAutomation.Tests.Common.Snapshots;
using SkiaSharp;

namespace ExplorerAutomation.Tests.Common.Visual;

/// <summary>
/// Frame-level synchronization for visual tests. Captures sequential frames and
/// blocks until N consecutive frames are pixel-stable so the snapshot shutter
/// fires after the scene has finished animating in / streaming assets / etc.
///
/// The check uses the same per-channel tolerance idea as the snapshot diff but
/// at a tighter default (4) — we want to know the picture has actually settled,
/// not just that it's "close enough" to the baseline.
/// </summary>
public static class Frame
{
    private const int CAPTURE_QUALITY = 100;

    public static void WaitForStable(
        int samples = 8,
        int intervalMs = 200,
        int perChannelTolerance = 4,
        int maxWaitMs = 6000)
    {
        AllureApi.Step($"Wait for {samples} stable frames", () =>
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            SKBitmap previous = null;
            var matched = 0;

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(intervalMs);
                    var current = ScreenshotCapture.CaptureBitmap(CAPTURE_QUALITY);

                    if (previous != null && PixelsClose(previous, current, perChannelTolerance))
                    {
                        matched++;
                        previous.Dispose();
                        previous = current;
                        if (matched >= samples - 1)
                        {
                            Reporter.Log($"Frame stabilized after {matched + 1} matching samples.");
                            return;
                        }
                    }
                    else
                    {
                        matched = 0;
                        previous?.Dispose();
                        previous = current;
                    }
                }

                Reporter.Log(
                    $"Frame did not stabilize within {maxWaitMs}ms (last match streak={matched}); proceeding anyway.");
            }
            finally
            {
                previous?.Dispose();
            }
        });
    }

    private static bool PixelsClose(SKBitmap a, SKBitmap b, int perChannelTolerance)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;

        var w = a.Width;
        var h = a.Height;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var pa = a.GetPixel(x, y);
                var pb = b.GetPixel(x, y);
                if (Math.Abs(pa.Red - pb.Red) > perChannelTolerance) return false;
                if (Math.Abs(pa.Green - pb.Green) > perChannelTolerance) return false;
                if (Math.Abs(pa.Blue - pb.Blue) > perChannelTolerance) return false;
            }
        }
        return true;
    }
}
