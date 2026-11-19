using FModel.Viewers.Core;
using SkiaSharp;
using Xunit;

namespace FModel.Tests;

public class ViewerCoreTests
{
    [Theory]
    [InlineData(0, 1, 5, 0.2, 0)]
    [InlineData(-3.3, 17.8, 6, 5, -0)]
    public void AxisTicks_PickNiceSteps(double min, double max, int target, double step, double start)
    {
        var ticks = AxisTicks.Compute(min, max, target);
        Assert.Equal(step, ticks.Step, 9);
        if (min == 0) Assert.Equal(start, ticks.Start, 9);
        Assert.True(ticks.Start >= min && ticks.Start - ticks.Step < min);
    }

    [Fact]
    public void AxisTicks_FormatQuarterStepsAndNegativeZero()
    {
        Assert.Equal("0.25", AxisTicks.Compute(0, 1, 4).Format(0.25));
        Assert.Equal("0.0", AxisTicks.Compute(0, 1, 5).Format(-1e-12));
    }

    private static ViewTransform FittedView()
    {
        var view = new ViewTransform { Plot = new SKRect(50, 10, 650, 410), FlipY = true };
        view.Fit(new WorldBounds(0, -1, 2, 1), uniform: false);
        return view;
    }

    [Fact]
    public void Fit_MapsBoundsOntoThePlot()
    {
        var view = FittedView();
        Assert.Equal(50, view.ToScreenX(0), 3);
        Assert.Equal(650, view.ToScreenX(2), 3);
        Assert.Equal(10, view.ToScreenY(1), 3);
        Assert.Equal(410, view.ToScreenY(-1), 3);
    }

    [Fact]
    public void ScreenWorld_RoundTrips()
    {
        var view = FittedView();
        Assert.Equal(1.234, view.ToWorldX(view.ToScreenX(1.234)), 4);
        Assert.Equal(-0.42, view.ToWorldY(view.ToScreenY(-0.42)), 4);
    }

    [Fact]
    public void ZoomAt_KeepsThePointUnderTheCursor()
    {
        var view = FittedView();
        var (wx, wy) = (view.ToWorldX(300), view.ToWorldY(200));
        view.ZoomAt(300, 200, 2, 0.5, 1e-3, 1e5);
        Assert.Equal(wx, view.ToWorldX(300), 6);
        Assert.Equal(wy, view.ToWorldY(200), 6);
    }

    [Fact]
    public void PanByScreen_MovesContentWithThePointer()
    {
        var view = FittedView();
        var (wx, wy) = (view.ToWorldX(300), view.ToWorldY(200));
        view.PanByScreen(30, -20);
        Assert.Equal(330, view.ToScreenX(wx), 3);
        Assert.Equal(180, view.ToScreenY(wy), 3);
    }

    [Fact]
    public void UniformFit_UsesTheSmallerScale()
    {
        var view = new ViewTransform { Plot = new SKRect(0, 0, 400, 100) };
        view.Fit(new WorldBounds(0, 0, 10, 10), uniform: true);
        Assert.Equal(view.ScaleX, view.ScaleY);
        Assert.Equal(10, view.ScaleX, 6);
    }
}
