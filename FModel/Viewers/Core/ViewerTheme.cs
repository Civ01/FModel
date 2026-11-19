using SkiaSharp;

namespace FModel.Viewers.Core;

/// <summary>
/// Colors used by Skia viewers. The WPF host fills it from the AdonisUI resources;
/// <see cref="Dark"/> matches FModel's default dark theme and is used headless.
/// </summary>
public sealed record ViewerTheme
{
    public SKColor Background { get; init; } = new(0x1E, 0x1F, 0x22);
    public SKColor PlotBackground { get; init; } = new(0x24, 0x25, 0x29);
    public SKColor GridMinor { get; init; } = new(0xFF, 0xFF, 0xFF, 0x10);
    public SKColor GridMajor { get; init; } = new(0xFF, 0xFF, 0xFF, 0x2A);
    public SKColor Axis { get; init; } = new(0xFF, 0xFF, 0xFF, 0x55);
    public SKColor Text { get; init; } = new(0xE6, 0xE6, 0xE6);
    public SKColor TextDim { get; init; } = new(0x9A, 0x9A, 0x9F);
    public SKColor Border { get; init; } = new(0x3A, 0x3B, 0x40);
    public SKColor CardBackground { get; init; } = new(0x2D, 0x2E, 0x33, 0xF2);
    public SKColor CardBorder { get; init; } = new(0x4A, 0x4B, 0x52);
    public SKColor Accent { get; init; } = new(0xB7, 0x9A, 0xF4);

    public string FontFamily { get; init; } = "Segoe UI";
    public float FontSize { get; init; } = 11f;

    public static ViewerTheme Dark { get; } = new();
}
