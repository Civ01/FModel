using CUE4Parse.UE4.Objects.Engine.Curves;

namespace FModel.Tests;

internal static class CurveTestUtil
{
    public static FRichCurveKey Key(float t, float v, ERichCurveInterpMode mode = ERichCurveInterpMode.RCIM_Linear,
        float arrive = 0, float leave = 0, ERichCurveTangentWeightMode weight = ERichCurveTangentWeightMode.RCTWM_WeightedNone,
        float arriveWeight = 0, float leaveWeight = 0) =>
        new()
        {
            Time = t, Value = v, InterpMode = mode, ArriveTangent = arrive, LeaveTangent = leave,
            TangentWeightMode = weight, ArriveTangentWeight = arriveWeight, LeaveTangentWeight = leaveWeight
        };

    public static FRichCurve Curve(ERichCurveExtrapolation pre, ERichCurveExtrapolation post, params FRichCurveKey[] keys) =>
        new() { Keys = keys, PreInfinityExtrap = pre, PostInfinityExtrap = post };
}
