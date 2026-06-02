using SkiaSharp;

namespace ExplorerAutomation.Tests.Common.Snapshots;

public sealed record SnapshotOptions
{
    public int PerChannelTolerance { get; init; } = 8;
    public SKSizeI? ForceSize { get; init; } = null;
    public int CaptureQuality { get; init; } = 100;
}
