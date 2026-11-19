using System;
using SkiaSharp;

namespace FModel.Viewers.Core;

/// <summary>
/// Axis-aligned world bounds used for fitting content into a viewport.
/// </summary>
public readonly record struct WorldBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;

    public WorldBounds Inflate(double fractionX, double fractionY)
    {
        var dx = Math.Max(Width, 1e-6) * fractionX;
        var dy = Math.Max(Height, 1e-6) * fractionY;
        return new WorldBounds(MinX - dx, MinY - dy, MaxX + dx, MaxY + dy);
    }

    /// <summary>
    /// Makes degenerate (zero width/height) bounds usable for fitting.
    /// </summary>
    public WorldBounds EnsureSize(double minWidth, double minHeight)
    {
        var (minX, maxX) = Expand(MinX, MaxX, minWidth);
        var (minY, maxY) = Expand(MinY, MaxY, minHeight);
        return new WorldBounds(minX, minY, maxX, maxY);
    }

    private static (double, double) Expand(double min, double max, double minSize)
    {
        if (max - min >= minSize) return (min, max);
        var mid = (min + max) * 0.5;
        return (mid - minSize * 0.5, mid + minSize * 0.5);
    }
}

/// <summary>
/// Maps world coordinates onto a screen-space plot rectangle (in DIPs).
/// Supports independent X/Y scales (curves) or uniform scale (graphs), and an upward Y axis.
/// </summary>
public sealed class ViewTransform
{
    /// <summary>Screen rectangle (DIPs) where world content is drawn.</summary>
    public SKRect Plot { get; set; }

    /// <summary>World X at <see cref="SKRect.Left"/> of the plot.</summary>
    public double OriginX { get; set; }

    /// <summary>World Y at the plot bottom (when <see cref="FlipY"/>) or top.</summary>
    public double OriginY { get; set; }

    /// <summary>Screen pixels per world unit.</summary>
    public double ScaleX { get; set; } = 1;
    public double ScaleY { get; set; } = 1;

    /// <summary>When true, world Y grows upward (math/curve convention).</summary>
    public bool FlipY { get; set; }

    /// <summary>Scale at the last fit, used to clamp zoom relative to the content.</summary>
    public double FitScaleX { get; private set; } = 1;
    public double FitScaleY { get; private set; } = 1;

    public bool HasBeenFitted { get; private set; }

    public float ToScreenX(double wx) => (float) (Plot.Left + (wx - OriginX) * ScaleX);
    public float ToScreenY(double wy) => FlipY
        ? (float) (Plot.Bottom - (wy - OriginY) * ScaleY)
        : (float) (Plot.Top + (wy - OriginY) * ScaleY);
    public SKPoint ToScreen(double wx, double wy) => new(ToScreenX(wx), ToScreenY(wy));

    public double ToWorldX(double sx) => OriginX + (sx - Plot.Left) / ScaleX;
    public double ToWorldY(double sy) => FlipY
        ? OriginY + (Plot.Bottom - sy) / ScaleY
        : OriginY + (sy - Plot.Top) / ScaleY;

    public double VisibleMinX => ToWorldX(Plot.Left);
    public double VisibleMaxX => ToWorldX(Plot.Right);
    public double VisibleMinY => FlipY ? ToWorldY(Plot.Bottom) : ToWorldY(Plot.Top);
    public double VisibleMaxY => FlipY ? ToWorldY(Plot.Top) : ToWorldY(Plot.Bottom);

    /// <summary>Moves the view by a screen-space delta (content follows the pointer).</summary>
    public void PanByScreen(double dx, double dy)
    {
        OriginX -= dx / ScaleX;
        OriginY += FlipY ? dy / ScaleY : -dy / ScaleY;
    }

    /// <summary>
    /// Zooms around a screen point so the world point under it stays fixed.
    /// <paramref name="minZoom"/>/<paramref name="maxZoom"/> are relative to the last fit scale.
    /// </summary>
    public void ZoomAt(double sx, double sy, double factorX, double factorY, double minZoom, double maxZoom)
    {
        var wx = ToWorldX(sx);
        var wy = ToWorldY(sy);

        ScaleX = Math.Clamp(ScaleX * factorX, FitScaleX * minZoom, FitScaleX * maxZoom);
        ScaleY = Math.Clamp(ScaleY * factorY, FitScaleY * minZoom, FitScaleY * maxZoom);

        OriginX = wx - (sx - Plot.Left) / ScaleX;
        OriginY = FlipY ? wy - (Plot.Bottom - sy) / ScaleY : wy - (sy - Plot.Top) / ScaleY;
    }

    /// <summary>Fits <paramref name="bounds"/> into the current plot rectangle.</summary>
    public void Fit(WorldBounds bounds, bool uniform)
    {
        var plotW = Math.Max(Plot.Width, 1);
        var plotH = Math.Max(Plot.Height, 1);
        var bw = Math.Max(bounds.Width, 1e-9);
        var bh = Math.Max(bounds.Height, 1e-9);

        var sx = plotW / bw;
        var sy = plotH / bh;
        if (uniform) sx = sy = Math.Min(sx, sy);

        ScaleX = FitScaleX = sx;
        ScaleY = FitScaleY = sy;

        // Center the bounds in the plot.
        var cx = (bounds.MinX + bounds.MaxX) * 0.5;
        var cy = (bounds.MinY + bounds.MaxY) * 0.5;
        OriginX = cx - plotW * 0.5 / ScaleX;
        OriginY = cy - plotH * 0.5 / ScaleY;
        HasBeenFitted = true;
    }

    public ViewTransform Clone() => (ViewTransform) MemberwiseClone();
}
