using SkiaSharp;

namespace ExplorerAutomation.Tests.Common.Snapshots;

internal static class ScreenshotCapture
{
    public static SKBitmap CaptureBitmap(int quality)
    {
        if (CommonStuff.AltDriver == null)
            throw new InvalidOperationException(
                "Cannot capture screenshot: AltDriver is not initialized.");

        var info = CommonStuff.AltDriver.GetScreenshot(
            size: default,
            screenShotQuality: quality);

        if (info.imageData == null || info.imageData.Length == 0)
            throw new InvalidOperationException(
                "AltTester returned an empty screenshot.");

        using var data = SKData.CreateCopy(info.imageData);
        var bmp = SKBitmap.Decode(data);
        if (bmp == null)
            throw new InvalidOperationException(
                $"Failed to decode AltTester screenshot ({info.imageData.Length} bytes).");

        return bmp;
    }

    public static byte[] EncodePng(SKBitmap bmp)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static SKBitmap Crop(SKBitmap source, SKRect clip)
    {
        var rect = SKRectI.Round(clip);
        var bounds = new SKRectI(0, 0, source.Width, source.Height);
        if (!bounds.IntersectsWith(rect))
            throw new ArgumentException(
                $"Clip rect {clip} does not intersect captured frame {bounds}.");

        // Clamp to image bounds so an over-sized clip still produces a valid crop.
        rect = SKRectI.Intersect(rect, bounds);

        var sub = new SKBitmap();
        if (!source.ExtractSubset(sub, rect))
        {
            sub.Dispose();
            throw new InvalidOperationException(
                $"ExtractSubset failed for rect {rect} on {source.Width}x{source.Height} bitmap.");
        }

        // ExtractSubset shares pixel memory with the source. Copy so the caller can dispose source.
        var copy = sub.Copy();
        sub.Dispose();
        return copy;
    }
}
