using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CUE4Parse.UE4.Objects.Engine.Curves;
using FModel.Viewers.Core;
using SkiaSharp;

namespace FModel.Viewers.Curves;

/// <summary>
/// Draws a <see cref="CurveAsset"/> like UE's curve editor: grid with time/value axes, one line per channel,
/// key markers, dashed pre/post infinity extrapolation and, for linear colors, a gradient strip.
/// Pure Skia, no WPF.
/// </summary>
public sealed class CurveScene : IViewerScene
{
    private const float LeftGutter = 58f;
    private const float RightGutter = 12f;
    private const float TopGutter = 10f;
    private const float BottomGutter = 24f;
    private const float GradientStripHeight = 14f;
    private const float GradientStripGap = 6f;
    private const float KeyRadius = 3.5f;
    private const float KeyHoverRadius = 5.5f;
    private const float KeyHitRadius = 7f;

    public CurveAsset Asset { get; }

    public event Action Changed;

    public bool FlipY => true;
    public bool UniformScale => false;
    public bool PanWithLeftButton => true;
    public double MinZoom => 1e-3;
    public double MaxZoom => 1e5;

    private SKPoint? _pointer;
    private (CurveChannel Channel, CurveKeyInfo Key)? _hoveredKey;

    public CurveScene(CurveAsset asset)
    {
        Asset = asset;
    }

    public IEnumerable<CurveChannel> VisibleChannels => Asset.Channels.Where(c => c.IsVisible);

    /// <summary>Call after toggling channel visibility.</summary>
    public void NotifyChannelsChanged()
    {
        if (_hoveredKey is { } hk && !hk.Channel.IsVisible) _hoveredKey = null;
        Changed?.Invoke();
    }

    public SKRect GetPlotRect(SKSize viewport)
    {
        var bottom = BottomGutter + (Asset.Kind == CurveAssetKind.LinearColor ? GradientStripHeight + GradientStripGap : 0f);
        var left = Math.Min(LeftGutter, viewport.Width * 0.4f);
        return new SKRect(left, TopGutter, Math.Max(left + 1, viewport.Width - RightGutter), Math.Max(TopGutter + 1, viewport.Height - bottom));
    }

    public WorldBounds? GetContentBounds()
    {
        var channels = VisibleChannels.ToList();
        if (channels.Count == 0) channels = Asset.Channels.ToList();
        if (channels.Count == 0) return null;

        var keyed = channels.Where(c => c.HasKeys).ToList();
        double minT, maxT;
        if (keyed.Count > 0)
        {
            minT = keyed.Min(c => c.FirstKeyTime);
            maxT = keyed.Max(c => c.LastKeyTime);
        }
        else
        {
            minT = 0;
            maxT = 1;
        }

        if (maxT - minT < 1e-6)
        {
            minT -= 0.5;
            maxT += 0.5;
        }

        // Sample inside the key range so cubic overshoot is framed too.
        var minV = double.MaxValue;
        var maxV = double.MinValue;
        const int samples = 512;
        foreach (var channel in channels)
        {
            foreach (var key in channel.Keys)
            {
                minV = Math.Min(minV, key.Value);
                maxV = Math.Max(maxV, key.Value);
            }

            for (var i = 0; i <= samples; i++)
            {
                var v = channel.Eval(minT + (maxT - minT) * i / samples);
                if (!float.IsFinite(v)) continue;
                minV = Math.Min(minV, v);
                maxV = Math.Max(maxV, v);
            }
        }

        if (minV > maxV)
        {
            minV = 0;
            maxV = 1;
        }

        var bounds = new WorldBounds(minT, minV, maxT, maxV)
            .EnsureSize(1e-6, Math.Max(Math.Abs(maxV) * 0.2, 0.2))
            .Inflate(0.04, 0.08);
        return bounds;
    }

    public bool UpdateHover(SKPoint? pointer, ViewTransform view)
    {
        var previousKey = _hoveredKey;
        var previousPointer = _pointer;
        _pointer = pointer;
        _hoveredKey = null;

        if (pointer is { } p)
        {
            var best = KeyHitRadius * KeyHitRadius;
            foreach (var channel in VisibleChannels)
            {
                foreach (var key in channel.Keys)
                {
                    var s = view.ToScreen(key.Time, key.Value);
                    var d = (s.X - p.X) * (s.X - p.X) + (s.Y - p.Y) * (s.Y - p.Y);
                    if (d > best) continue;
                    best = d;
                    _hoveredKey = (channel, key);
                }
            }
        }

        // Any pointer move inside the plot changes the cursor readout, so redraw whenever something moved.
        return !Nullable.Equals(previousPointer, _pointer) || !Equals(previousKey, _hoveredKey);
    }

    public void Render(SKCanvas canvas, SKSize viewport, ViewTransform view, ViewerTheme theme)
    {
        var plot = view.Plot;
        var typeface = Typefaces.Get(theme.FontFamily);
        using var text = new SKPaint { IsAntialias = true, Color = theme.TextDim, TextSize = theme.FontSize, Typeface = typeface };

        canvas.Clear(theme.Background);
        using (var plotBg = new SKPaint { Color = theme.PlotBackground })
            canvas.DrawRect(plot, plotBg);

        var xTicks = AxisTicks.Compute(view.VisibleMinX, view.VisibleMaxX, Math.Max(2, (int) (plot.Width / 90)));
        var yTicks = AxisTicks.Compute(view.VisibleMinY, view.VisibleMaxY, Math.Max(2, (int) (plot.Height / 45)));

        canvas.Save();
        canvas.ClipRect(plot);
        DrawGrid(canvas, view, theme, xTicks, yTicks);

        var channels = VisibleChannels.ToList();
        foreach (var channel in channels)
            DrawChannel(canvas, view, channel);
        foreach (var channel in channels)
            DrawKeys(canvas, view, theme, channel);
        DrawCursor(canvas, view, theme);
        canvas.Restore();

        using (var border = new SKPaint { Color = theme.Border, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            canvas.DrawRect(plot, border);

        DrawAxisLabels(canvas, view, text, xTicks, yTicks);

        if (Asset.Kind == CurveAssetKind.LinearColor)
            DrawGradientStrip(canvas, view, theme);

        if (channels.Count == 0)
        {
            using var hint = new SKPaint { IsAntialias = true, Color = theme.TextDim, TextSize = theme.FontSize + 2, Typeface = typeface, TextAlign = SKTextAlign.Center };
            canvas.DrawText("No channels visible", plot.MidX, plot.MidY, hint);
        }
        else if (channels.All(c => !c.HasKeys))
        {
            using var hint = new SKPaint { IsAntialias = true, Color = theme.TextDim, TextSize = theme.FontSize, Typeface = typeface };
            canvas.DrawText("No keys — showing the channels' default value", plot.Left + 8, plot.Top + 16, hint);
        }

        DrawHoverCard(canvas, viewport, view, theme, typeface);
    }

    private static void DrawGrid(SKCanvas canvas, ViewTransform view, ViewerTheme theme, AxisTicks.Ticks xTicks, AxisTicks.Ticks yTicks)
    {
        var plot = view.Plot;
        using var minor = new SKPaint { Color = theme.GridMinor, StrokeWidth = 1 };
        using var major = new SKPaint { Color = theme.GridMajor, StrokeWidth = 1 };

        foreach (var t in xTicks.Values())
        {
            var x = Snap(view.ToScreenX(t));
            canvas.DrawLine(x, plot.Top, x, plot.Bottom, IsZero(t, xTicks.Step) ? major : minor);
        }

        foreach (var v in yTicks.Values())
        {
            var y = Snap(view.ToScreenY(v));
            canvas.DrawLine(plot.Left, y, plot.Right, y, IsZero(v, yTicks.Step) ? major : minor);
        }
    }

    private static void DrawAxisLabels(SKCanvas canvas, ViewTransform view, SKPaint text, AxisTicks.Ticks xTicks, AxisTicks.Ticks yTicks)
    {
        var plot = view.Plot;
        var metrics = text.FontMetrics;
        var textHeight = metrics.Descent - metrics.Ascent;

        text.TextAlign = SKTextAlign.Center;
        foreach (var t in xTicks.Values())
        {
            var x = view.ToScreenX(t);
            if (x < plot.Left - 0.5f || x > plot.Right + 0.5f) continue;
            canvas.DrawText(xTicks.Format(t), x, plot.Bottom + 4 - metrics.Ascent, text);
        }

        text.TextAlign = SKTextAlign.Right;
        foreach (var v in yTicks.Values())
        {
            var y = view.ToScreenY(v);
            if (y < plot.Top - 0.5f || y > plot.Bottom + 0.5f) continue;
            canvas.DrawText(yTicks.Format(v), plot.Left - 6, y + textHeight * 0.5f - metrics.Descent, text);
        }
        text.TextAlign = SKTextAlign.Left;
    }

    private static void DrawChannel(SKCanvas canvas, ViewTransform view, CurveChannel channel)
    {
        var plot = view.Plot;
        var minT = view.ToWorldX(plot.Left - 2);
        var maxT = view.ToWorldX(plot.Right + 2);
        if (!(maxT > minT)) return;

        using var solid = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.75f,
            StrokeJoin = SKStrokeJoin.Round,
            Color = channel.Color
        };
        using var dashEffect = SKPathEffect.CreateDash([6f, 4f], 0);
        using var dashed = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = channel.Color.WithAlpha(150),
            PathEffect = dashEffect
        };

        if (!channel.HasKeys)
        {
            // Constant everywhere: the default value (or 0 when unset).
            var y = view.ToScreenY(channel.Eval(0));
            canvas.DrawLine(plot.Left, y, plot.Right, y, dashed);
            return;
        }

        var times = BuildSampleTimes(channel, view, minT, maxT);
        var first = channel.FirstKeyTime;
        var last = channel.LastKeyTime;

        // Pre-infinity, key range, post-infinity. Boundary samples are shared so the pieces join up.
        DrawPolyline(canvas, view, channel, times.Where(t => t <= first), dashed);
        DrawPolyline(canvas, view, channel, times.Where(t => t >= first && t <= last), solid);
        DrawPolyline(canvas, view, channel, times.Where(t => t >= last), dashed);
    }

    /// <summary>
    /// One sample per screen pixel plus every key time (and a sample just before it) so constant steps
    /// and sharp tangent breaks land exactly on the keys.
    /// </summary>
    private static List<double> BuildSampleTimes(CurveChannel channel, ViewTransform view, double minT, double maxT)
    {
        var pixelStep = 1.0 / view.ScaleX;
        var count = (int) Math.Min((maxT - minT) / pixelStep, 20000);
        var times = new List<double>(count + channel.Keys.Count * 2 + 4);
        for (var i = 0; i <= count; i++)
            times.Add(minT + i * pixelStep);
        times.Add(maxT);

        foreach (var key in channel.Keys)
        {
            if (key.Time < minT || key.Time > maxT) continue;
            var epsilon = Math.Max(Math.Abs(key.Time), 1.0) * 1e-5;
            times.Add(key.Time - epsilon);
            times.Add(key.Time);
        }

        // Make sure the key range boundaries exist even when keys are off-screen.
        if (channel.FirstKeyTime > minT && channel.FirstKeyTime < maxT) times.Add(channel.FirstKeyTime);
        if (channel.LastKeyTime > minT && channel.LastKeyTime < maxT) times.Add(channel.LastKeyTime);

        times.Sort();
        return times;
    }

    private static void DrawPolyline(SKCanvas canvas, ViewTransform view, CurveChannel channel, IEnumerable<double> times, SKPaint paint)
    {
        using var path = new SKPath();
        var started = false;
        foreach (var t in times)
        {
            var v = channel.Eval(t);
            if (!float.IsFinite(v))
            {
                started = false;
                continue;
            }

            // Clamp to a sane screen range so huge values don't overflow Skia's float path math.
            var x = view.ToScreenX(t);
            var y = Math.Clamp(view.ToScreenY(v), -1e6f, 1e6f);
            if (!started)
            {
                path.MoveTo(x, y);
                started = true;
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        if (path.PointCount > 1)
            canvas.DrawPath(path, paint);
    }

    private void DrawKeys(SKCanvas canvas, ViewTransform view, ViewerTheme theme, CurveChannel channel)
    {
        using var fill = new SKPaint { IsAntialias = true, Color = channel.Color };
        using var outline = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = theme.PlotBackground };
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = theme.Text };

        var plot = view.Plot;
        foreach (var key in channel.Keys)
        {
            var p = view.ToScreen(key.Time, key.Value);
            if (p.X < plot.Left - 10 || p.X > plot.Right + 10 || p.Y < plot.Top - 10 || p.Y > plot.Bottom + 10) continue;

            var hovered = _hoveredKey is { } hk && hk.Channel == channel && hk.Key.Index == key.Index;
            var radius = hovered ? KeyHoverRadius : KeyRadius;
            canvas.DrawCircle(p, radius, fill);
            canvas.DrawCircle(p, radius, hovered ? ring : outline);
        }
    }

    private void DrawCursor(SKCanvas canvas, ViewTransform view, ViewerTheme theme)
    {
        if (_pointer is not { } p || _hoveredKey != null || !view.Plot.Contains(p)) return;

        using var line = new SKPaint { Color = theme.Axis, StrokeWidth = 1 };
        var x = Snap(p.X);
        canvas.DrawLine(x, view.Plot.Top, x, view.Plot.Bottom, line);

        var t = view.ToWorldX(p.X);
        foreach (var channel in VisibleChannels)
        {
            var v = channel.Eval(t);
            if (!float.IsFinite(v)) continue;
            using var dot = new SKPaint { IsAntialias = true, Color = channel.Color };
            canvas.DrawCircle(x, view.ToScreenY(v), 2.5f, dot);
        }
    }

    private void DrawGradientStrip(SKCanvas canvas, ViewTransform view, ViewerTheme theme)
    {
        var plot = view.Plot;
        var strip = new SKRect(plot.Left, plot.Bottom + BottomGutter, plot.Right, plot.Bottom + BottomGutter + GradientStripHeight);

        // Checkerboard so alpha is visible.
        using (var light = new SKPaint { Color = new SKColor(0x9A, 0x9A, 0x9A) })
        using (var dark = new SKPaint { Color = new SKColor(0x66, 0x66, 0x66) })
        {
            const float cell = 7f;
            canvas.Save();
            canvas.ClipRect(strip);
            for (var x = strip.Left; x < strip.Right; x += cell)
            for (var y = strip.Top; y < strip.Bottom; y += cell)
            {
                var odd = ((int) ((x - strip.Left) / cell) + (int) ((y - strip.Top) / cell)) % 2 == 1;
                canvas.DrawRect(x, y, cell, cell, odd ? dark : light);
            }
            canvas.Restore();
        }

        using var column = new SKPaint { IsAntialias = false };
        for (var x = strip.Left; x < strip.Right; x += 1f)
        {
            var (r, g, b, a) = Asset.EvalColor(view.ToWorldX(x + 0.5f));
            column.Color = new SKColor(ToSrgbByte(r), ToSrgbByte(g), ToSrgbByte(b), ToByte(a));
            canvas.DrawRect(x, strip.Top, 1f, strip.Height, column);
        }

        using var border = new SKPaint { Color = theme.Border, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(strip, border);
    }

    private void DrawHoverCard(SKCanvas canvas, SKSize viewport, ViewTransform view, ViewerTheme theme, SKTypeface typeface)
    {
        if (_pointer is not { } p) return;

        var lines = new List<(string Text, SKColor Color)>();
        if (_hoveredKey is { } hk)
        {
            var key = hk.Key;
            lines.Add(($"{ChannelLabel(hk.Channel)}  ·  key {key.Index + 1}/{hk.Channel.Keys.Count}", hk.Channel.Color));
            lines.Add(($"Time   {FormatNumber(key.Time)}", theme.Text));
            lines.Add(($"Value  {FormatNumber(key.Value)}", theme.Text));
            lines.Add(($"Interp {InterpName(key.InterpMode)}{(key.TangentMode is { } tm ? $" ({TangentName(tm)})" : "")}", theme.TextDim));
            if (key.ArriveTangent is { } arrive && key.LeaveTangent is { } leave)
                lines.Add(($"Tangents  in {FormatNumber(arrive)}  out {FormatNumber(leave)}", theme.TextDim));
        }
        else if (view.Plot.Contains(p))
        {
            var t = view.ToWorldX(p.X);
            lines.Add(($"Time  {FormatNumber(t)}", theme.Text));
            foreach (var channel in VisibleChannels)
                lines.Add(($"{ChannelLabel(channel)}  {FormatNumber(channel.Eval(t))}", channel.Color));
        }

        if (lines.Count == 0) return;

        using var paint = new SKPaint { IsAntialias = true, TextSize = theme.FontSize, Typeface = typeface };
        var metrics = paint.FontMetrics;
        var lineHeight = metrics.Descent - metrics.Ascent + 2;
        const float pad = 7f;
        var width = lines.Max(l => paint.MeasureText(l.Text)) + pad * 2;
        var height = lineHeight * lines.Count + pad * 2 - 2;

        var x = p.X + 14;
        var y = p.Y + 14;
        if (x + width > viewport.Width - 4) x = p.X - 14 - width;
        if (y + height > viewport.Height - 4) y = p.Y - 14 - height;
        x = Math.Max(4, x);
        y = Math.Max(4, y);

        var rect = SKRect.Create(x, y, width, height);
        using (var bg = new SKPaint { IsAntialias = true, Color = theme.CardBackground })
            canvas.DrawRoundRect(rect, 4, 4, bg);
        using (var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = theme.CardBorder })
            canvas.DrawRoundRect(rect, 4, 4, border);

        var baseline = y + pad - metrics.Ascent;
        foreach (var (lineText, color) in lines)
        {
            paint.Color = color;
            canvas.DrawText(lineText, x + pad, baseline, paint);
            baseline += lineHeight;
        }
    }

    private string ChannelLabel(CurveChannel channel) => Asset.Kind == CurveAssetKind.Float ? "Value" : channel.Name;

    public static string FormatNumber(double value)
    {
        if (!double.IsFinite(value)) return value.ToString(CultureInfo.InvariantCulture);
        var abs = Math.Abs(value);
        if (abs != 0 && (abs < 1e-4 || abs >= 1e7))
            return value.ToString("0.###E+0", CultureInfo.InvariantCulture);
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    public static string InterpName(ERichCurveInterpMode mode) => mode switch
    {
        ERichCurveInterpMode.RCIM_Linear => "Linear",
        ERichCurveInterpMode.RCIM_Constant => "Constant",
        ERichCurveInterpMode.RCIM_Cubic => "Cubic",
        ERichCurveInterpMode.RCIM_None => "None",
        _ => mode.ToString()
    };

    public static string TangentName(ERichCurveTangentMode mode) => mode switch
    {
        ERichCurveTangentMode.RCTM_Auto => "Auto",
        ERichCurveTangentMode.RCTM_User => "User",
        ERichCurveTangentMode.RCTM_Break => "Break",
        ERichCurveTangentMode.RCTM_None => "None",
        _ => mode.ToString()
    };

    private static bool IsZero(double value, double step) => Math.Abs(value) < step * 1e-6;
    private static float Snap(float v) => MathF.Floor(v) + 0.5f;

    private static byte ToByte(float v) => (byte) Math.Clamp(MathF.Round(v * 255f), 0f, 255f);

    /// <summary>Linear → sRGB transfer, as UE's FLinearColor::ToFColor(true).</summary>
    private static byte ToSrgbByte(float linear)
    {
        linear = Math.Clamp(linear, 0f, 1f);
        var srgb = linear <= 0.0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
        return ToByte(srgb);
    }
}
