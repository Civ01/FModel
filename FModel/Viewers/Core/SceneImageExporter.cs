using System;
using SkiaSharp;

namespace FModel.Viewers.Core;

/// <summary>Renders a scene off-screen to PNG bytes (no UI involved).</summary>
public static class SceneImageExporter
{
    public enum Area
    {
        /// <summary>Exactly what the viewport shows.</summary>
        Viewport,
        /// <summary>The scene's whole content, fitted into the same output size.</summary>
        Content
    }

    /// <param name="viewportSize">Size in DIPs of the on-screen view.</param>
    /// <param name="scale">Output pixels per DIP (2 = sharp on high-DPI and when zoomed).</param>
    public static byte[] RenderPng(IViewerScene scene, ViewTransform current, SKSize viewportSize, ViewerTheme theme, Area area, float scale = 2f)
    {
        var width = Math.Max(1, (int) Math.Ceiling(viewportSize.Width * scale));
        var height = Math.Max(1, (int) Math.Ceiling(viewportSize.Height * scale));

        var view = current.Clone();
        view.Plot = scene.GetPlotRect(viewportSize);
        if (area == Area.Content && scene.GetContentBounds() is { } bounds)
            view.Fit(bounds, scene.UniformScale);

        // The pointer isn't part of an export.
        scene.UpdateHover(null, view);

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Scale(scale);
        scene.Render(canvas, viewportSize, view, theme);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
