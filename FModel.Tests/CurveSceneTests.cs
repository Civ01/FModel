using System;
using System.IO;
using CUE4Parse.UE4.Objects.Engine.Curves;
using FModel.Viewers.Core;
using FModel.Viewers.Curves;
using SkiaSharp;
using Xunit;
using static CUE4Parse.UE4.Objects.Engine.Curves.ERichCurveExtrapolation;
using static CUE4Parse.UE4.Objects.Engine.Curves.ERichCurveInterpMode;
using static FModel.Tests.CurveTestUtil;

namespace FModel.Tests;

/// <summary>Render smoke tests. PNGs go to TestResults/curve-renders for eyeballing (CI uploads them).</summary>
public class CurveSceneTests
{
    private static readonly SKSize Viewport = new(900, 380);
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "curve-renders");

    public static CurveAsset FloatSample() => CurveAsset.FromChannels("CF_Sample", CurveAssetKind.Float,
    [
        new CurveChannel("Value", Curve(RCCE_Oscillate, RCCE_Cycle,
            Key(0, 0, RCIM_Cubic, 0, 2), Key(0.6f, 1.2f, RCIM_Cubic), Key(1.0f, 0.4f, RCIM_Constant), Key(1.4f, 0.9f), Key(2f, 0.2f, RCIM_Cubic)),
            CurveAsset.FloatColor)
    ]);

    public static CurveAsset VectorSample() => CurveAsset.FromChannels("CV_Sample", CurveAssetKind.Vector,
    [
        new CurveChannel("X", Curve(RCCE_Constant, RCCE_Linear, Key(0, 0, RCIM_Cubic), Key(1, 10, RCIM_Cubic)), CurveAsset.RedColor),
        new CurveChannel("Y", Curve(RCCE_CycleWithOffset, RCCE_CycleWithOffset, Key(0, 2), Key(0.5f, 6), Key(1, 4)), CurveAsset.GreenColor),
        new CurveChannel("Z", new FRichCurve(), CurveAsset.BlueColor)
    ]);

    public static CurveAsset ColorSample() => CurveAsset.FromChannels("CC_Sample", CurveAssetKind.LinearColor,
    [
        new CurveChannel("R", Curve(RCCE_Constant, RCCE_Constant, Key(0, 1), Key(0.5f, 0.2f), Key(1, 0)), CurveAsset.RedColor),
        new CurveChannel("G", Curve(RCCE_Constant, RCCE_Constant, Key(0, 0), Key(0.5f, 0.8f), Key(1, 0.3f)), CurveAsset.GreenColor),
        new CurveChannel("B", Curve(RCCE_Constant, RCCE_Constant, Key(0, 0.1f), Key(1, 1)), CurveAsset.BlueColor),
        new CurveChannel("A", Curve(RCCE_Constant, RCCE_Constant, Key(0, 1), Key(1, 0.2f)), CurveAsset.AlphaColor)
    ]);

    public static TheoryData<string> Samples => ["float", "vector", "color", "empty"];

    private static CurveAsset Sample(string name) => name switch
    {
        "float" => FloatSample(),
        "vector" => VectorSample(),
        "color" => ColorSample(),
        _ => CurveAsset.FromChannels("CF_Empty", CurveAssetKind.Float, [new CurveChannel("Value", new FRichCurve(), CurveAsset.FloatColor)])
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Renders_AndExports(string sample)
    {
        var scene = new CurveScene(Sample(sample));
        var view = new ViewTransform { FlipY = scene.FlipY, Plot = scene.GetPlotRect(Viewport) };
        var bounds = scene.GetContentBounds();
        Assert.NotNull(bounds);
        view.Fit(bounds.Value, scene.UniformScale);
        view.ZoomAt(view.Plot.MidX, view.Plot.MidY, 0.55, 1, scene.MinZoom, scene.MaxZoom); // show extrapolation

        // Hover over the middle of the plot so the readout card is drawn too.
        scene.UpdateHover(new SKPoint(view.Plot.MidX, view.Plot.MidY), view);
        var png = SceneImageExporter.RenderPng(scene, view, Viewport, ViewerTheme.Dark, SceneImageExporter.Area.Viewport);

        Assert.True(png.Length > 1000);
        using var bitmap = SKBitmap.Decode(png);
        Assert.Equal((int) Viewport.Width * 2, bitmap.Width);
        Assert.Equal((int) Viewport.Height * 2, bitmap.Height);

        Directory.CreateDirectory(OutputDir);
        File.WriteAllBytes(Path.Combine(OutputDir, $"{sample}.png"), png);
    }

    [Fact]
    public void Hover_FindsTheNearestKey()
    {
        var scene = new CurveScene(FloatSample());
        var view = new ViewTransform { FlipY = true, Plot = scene.GetPlotRect(Viewport) };
        view.Fit(scene.GetContentBounds()!.Value, false);

        var keyOnScreen = view.ToScreen(0.6, 1.2);
        Assert.True(scene.UpdateHover(new SKPoint(keyOnScreen.X + 3, keyOnScreen.Y - 2), view));
    }

    [Fact]
    public void ContentBounds_IncludeCubicOvershootAndAllKeys()
    {
        var scene = new CurveScene(VectorSample());
        var bounds = scene.GetContentBounds()!.Value;
        Assert.True(bounds.MinX < 0 && bounds.MaxX > 1);
        Assert.True(bounds.MinY < 0 && bounds.MaxY > 10);
    }

    [Fact]
    public void HiddenChannels_AreLeftOutOfTheFit()
    {
        var asset = VectorSample();
        var scene = new CurveScene(asset);
        asset.Channels[0].IsVisible = false; // X goes up to 10
        scene.NotifyChannelsChanged();
        Assert.True(scene.GetContentBounds()!.Value.MaxY < 9);
    }
}
