using SkiaSharp;

namespace ExplorerAutomation.Tests.Common.Snapshots;

public sealed record ImageDiffResult(
    bool Success,
    long DifferingPixels,
    long TotalPixels,
    double MismatchPercent,
    SKBitmap DiffBitmap);
