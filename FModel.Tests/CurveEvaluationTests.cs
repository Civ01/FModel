using CUE4Parse.UE4.Objects.Engine.Curves;
using Xunit;
using static CUE4Parse.UE4.Objects.Engine.Curves.ERichCurveExtrapolation;
using static CUE4Parse.UE4.Objects.Engine.Curves.ERichCurveInterpMode;
using static FModel.Tests.CurveTestUtil;

namespace FModel.Tests;

/// <summary>
/// FRichCurve.Eval must match UE 5.x (RealCurve.cpp, RichCurve.cpp, CurveEvaluation.cpp).
/// </summary>
public class CurveEvaluationTests
{
    private const int Precision = 4;

    [Theory]
    [InlineData(1.5f, 0.5f)]
    [InlineData(2.25f, 0.25f)]
    [InlineData(-0.25f, 0.75f)]
    [InlineData(-1.75f, 0.25f)]
    public void Cycle_RepeatsTheKeyRange(float time, float expected)
    {
        var curve = Curve(RCCE_Cycle, RCCE_Cycle, Key(0, 0), Key(1, 1));
        Assert.Equal(expected, curve.Eval(time), Precision);
    }

    [Theory]
    [InlineData(1.5f, 1.5f)]
    [InlineData(2.5f, 2.5f)]
    [InlineData(-0.5f, -0.5f)]
    public void CycleWithOffset_AccumulatesTheValueDelta(float time, float expected)
    {
        var curve = Curve(RCCE_CycleWithOffset, RCCE_CycleWithOffset, Key(0, 0), Key(1, 1));
        Assert.Equal(expected, curve.Eval(time), Precision);
    }

    [Theory]
    [InlineData(1.25f, 0.75f)]
    [InlineData(2.25f, 0.25f)]
    [InlineData(-0.25f, 0.25f)]
    public void Oscillate_MirrorsEveryOtherCycle(float time, float expected)
    {
        var curve = Curve(RCCE_Oscillate, RCCE_Oscillate, Key(0, 0), Key(1, 1));
        Assert.Equal(expected, curve.Eval(time), Precision);
    }

    [Fact]
    public void Linear_UsesTheSlopeOfTheEdgeKeys()
    {
        var curve = Curve(RCCE_Linear, RCCE_Linear, Key(0, 0), Key(1, 2));
        Assert.Equal(4f, curve.Eval(2f), Precision);
        Assert.Equal(-2f, curve.Eval(-1f), Precision);
    }

    [Fact]
    public void Constant_HoldsTheEdgeValues()
    {
        var curve = Curve(RCCE_Constant, RCCE_Constant, Key(0, 3), Key(1, 5));
        Assert.Equal(5f, curve.Eval(10f), Precision);
        Assert.Equal(3f, curve.Eval(-10f), Precision);
    }

    [Fact]
    public void Cycle_WithZeroDuration_DoesNotThrow()
    {
        var curve = Curve(RCCE_Cycle, RCCE_Cycle, Key(1, 2), Key(1, 2));
        Assert.Equal(2f, curve.Eval(5f), Precision);
        Assert.Equal(2f, curve.Eval(-5f), Precision);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0.999f, 1f)]
    [InlineData(1f, 2f)]
    [InlineData(2f, 3f)]
    public void ConstantKeys_HoldUntilTheNextKeyTime(float time, float expected)
    {
        var curve = Curve(RCCE_Constant, RCCE_Constant, Key(0, 1, RCIM_Constant), Key(1, 2, RCIM_Constant), Key(2, 3, RCIM_Constant));
        Assert.Equal(expected, curve.Eval(time), Precision);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    public void WeightedTangents_WithDefaultWeights_MatchUnweighted(float time)
    {
        // Flat tangents with weight = 1/3 of the key distance are the same bezier as the unweighted path.
        const float w = 1f / 3f;
        var unweighted = Curve(RCCE_Constant, RCCE_Constant, Key(0, 0, RCIM_Cubic), Key(1, 1, RCIM_Cubic));
        var weighted = Curve(RCCE_Constant, RCCE_Constant,
            Key(0, 0, RCIM_Cubic, weight: ERichCurveTangentWeightMode.RCTWM_WeightedBoth, arriveWeight: w, leaveWeight: w),
            Key(1, 1, RCIM_Cubic, weight: ERichCurveTangentWeightMode.RCTWM_WeightedBoth, arriveWeight: w, leaveWeight: w));

        Assert.Equal(unweighted.Eval(time), weighted.Eval(time), 2);
    }
}
