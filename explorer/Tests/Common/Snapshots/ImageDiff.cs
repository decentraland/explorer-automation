using System.Runtime.InteropServices;
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

        // Read both frames into straight (un-premultiplied) RGBA8888 byte buffers in a single
        // native bulk conversion each. ReadPixels normalizes whatever color type the PNG/JPEG
        // decoded to, so the R/G/B values match the old GetPixel() semantics exactly — but this
        // avoids ~2M per-pixel interop calls per comparison (×2 in record mode, ×~20 fixtures).
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var baseBytes = ReadStraightRgba(baseline, info);
        var actualBytes = ReadStraightRgba(actual, info);
        var diffBytes = new byte[baseBytes.Length];

        for (var i = 0; i < baseBytes.Length; i += 4)
        {
            var dR = Math.Abs(baseBytes[i] - actualBytes[i]);
            var dG = Math.Abs(baseBytes[i + 1] - actualBytes[i + 1]);
            var dB = Math.Abs(baseBytes[i + 2] - actualBytes[i + 2]);
            var max = Math.Max(dR, Math.Max(dG, dB));

            if (max > perChannelTolerance)
            {
                differing++;
                diffBytes[i] = 255;     // R
                diffBytes[i + 1] = 0;   // G
                diffBytes[i + 2] = 0;   // B
                diffBytes[i + 3] = 255; // A
            }
            else
            {
                diffBytes[i] = (byte)(actualBytes[i] / 2);
                diffBytes[i + 1] = (byte)(actualBytes[i + 1] / 2);
                diffBytes[i + 2] = (byte)(actualBytes[i + 2] / 2);
                diffBytes[i + 3] = 255;
            }
        }

        // All diff pixels are fully opaque (alpha 255), so a Premul bitmap stores these straight
        // bytes unchanged — same output the old SetPixel path produced.
        var diff = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        Marshal.Copy(diffBytes, 0, diff.GetPixels(), diffBytes.Length);

        var pct = (double)differing / total * 100.0;
        return new ImageDiffResult(
            Success: pct <= maxDifferingPixelPercent,
            DifferingPixels: differing,
            TotalPixels: total,
            MismatchPercent: pct,
            DiffBitmap: diff);
    }

    private static byte[] ReadStraightRgba(SKBitmap bmp, SKImageInfo info)
    {
        var buffer = new byte[info.BytesSize];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            using var pixmap = bmp.PeekPixels();
            if (pixmap is null ||
                !pixmap.ReadPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, 0, 0))
                throw new InvalidOperationException(
                    $"Failed to read {bmp.Width}x{bmp.Height} bitmap pixels for image diff.");
        }
        finally
        {
            handle.Free();
        }
        return buffer;
    }
}
