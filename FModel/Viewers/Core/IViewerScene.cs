using System;
using SkiaSharp;

namespace FModel.Viewers.Core;

/// <summary>
/// Something a <c>SkiaCanvasView</c> can draw, pan, zoom and hit-test.
/// Implementations must not depend on WPF so they can be rendered headless (PNG export, tests).
/// </summary>
public interface IViewerScene
{
    /// <summary>Raised when the scene needs a redraw (data or hover changed).</summary>
    event Action Changed;

    /// <summary>World Y grows upward.</summary>
    bool FlipY { get; }

    /// <summary>Keep X and Y scale equal (graphs) or independent (curves).</summary>
    bool UniformScale { get; }

    /// <summary>Left-button drag pans the view (no selection in this scene).</summary>
    bool PanWithLeftButton { get; }

    /// <summary>Zoom limits relative to the fit scale.</summary>
    double MinZoom { get; }
    double MaxZoom { get; }

    /// <summary>Screen rectangle (DIPs) for world content; the rest is chrome such as axis labels.</summary>
    SKRect GetPlotRect(SKSize viewport);

    /// <summary>Bounds that "Fit to view" frames, or null when there is nothing to show.</summary>
    WorldBounds? GetContentBounds();

    void Render(SKCanvas canvas, SKSize viewport, ViewTransform view, ViewerTheme theme);

    /// <summary>Updates hover state for a pointer position (DIPs) or null when the pointer left. Returns true if a redraw is needed.</summary>
    bool UpdateHover(SKPoint? pointer, ViewTransform view);
}
