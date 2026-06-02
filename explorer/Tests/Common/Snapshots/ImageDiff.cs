using SkiaSharp;

namespace ExplorerAutomation.Tests.Common.Snapshots;

public static class ImageDiff
{
    public static ImageDiffResult Compare(
        SKBitmap baseline,
        SKBitmap actual,
        int perChannelTolerance,
        double maxDifferingPixelPercent)
    {
        if (baseline.Width != actual.Width || baseline.Height != actual.Height)
            throw new ArgumentException(
                $"Size mismatch: baseline {baseline.Width}x{baseline.Height} vs actual {actual.Width}x{actual.Height}");

        var w = baseline.Width;
        var h = baseline.Height;
        var total = (long)w * h;
        var differing = 0L;

        var diff = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var red = new SKColor(255, 0, 0, 255);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var b = baseline.GetPixel(x, y);
                var a = actual.GetPixel(x, y);

                var dR = Math.Abs(b.Red - a.Red);
                var dG = Math.Abs(b.Green - a.Green);
                var dB = Math.Abs(b.Blue - a.Blue);
                var max = Math.Max(dR, Math.Max(dG, dB));

                if (max > perChannelTolerance)
                {
                    differing++;
                    diff.SetPixel(x, y, red);
                }
                else
                {
                    diff.SetPixel(x, y, new SKColor(
                        (byte)(a.Red / 2),
                        (byte)(a.Green / 2),
                        (byte)(a.Blue / 2),
                        255));
                }
            }
        }

        var pct = (double)differing / total * 100.0;
        return new ImageDiffResult(
            Success: pct <= maxDifferingPixelPercent,
            DifferingPixels: differing,
            TotalPixels: total,
            MismatchPercent: pct,
            DiffBitmap: diff);
    }
}
