using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine.Curves;
using SkiaSharp;

namespace FModel.Viewers.Curves;

public enum CurveAssetKind
{
    Float,
    Vector,
    LinearColor
}

/// <summary>Read-only snapshot of one key, independent of the curve flavour.</summary>
public readonly record struct CurveKeyInfo(
    int Index,
    float Time,
    float Value,
    ERichCurveInterpMode InterpMode,
    ERichCurveTangentMode? TangentMode,
    float? ArriveTangent,
    float? LeaveTangent);

public sealed class CurveChannel
{
    public string Name { get; }
    public FRealCurve Curve { get; }
    public SKColor Color { get; }
    public IReadOnlyList<CurveKeyInfo> Keys { get; }
    public bool IsVisible { get; set; } = true;

    public bool HasKeys => Keys.Count > 0;
    public float FirstKeyTime => HasKeys ? Keys[0].Time : 0f;
    public float LastKeyTime => HasKeys ? Keys[^1].Time : 0f;

    /// <summary>True when the curve evaluates to its default value everywhere (no keys and a set default).</summary>
    public bool HasDefaultValueOnly => !HasKeys && Curve.DefaultValue != float.MaxValue;

    public CurveChannel(string name, FRealCurve curve, SKColor color)
    {
        Name = name;
        Curve = curve;
        Color = color;
        Keys = ExtractKeys(curve);
    }

    /// <summary>Evaluates the channel the way UE does, including pre/post infinity extrapolation.</summary>
    public float Eval(double time) => Curve.Eval((float) time);

    private static IReadOnlyList<CurveKeyInfo> ExtractKeys(FRealCurve curve)
    {
        switch (curve)
        {
            case FRichCurve rich when rich.Keys is { Length: > 0 }:
                return rich.Keys.Select((k, i) => new CurveKeyInfo(i, k.Time, k.Value, k.InterpMode,
                    k.InterpMode == ERichCurveInterpMode.RCIM_Cubic ? k.TangentMode : null,
                    k.InterpMode == ERichCurveInterpMode.RCIM_Cubic ? k.ArriveTangent : null,
                    k.InterpMode == ERichCurveInterpMode.RCIM_Cubic ? k.LeaveTangent : null)).ToArray();
            case FSimpleCurve simple when simple.Keys is { Length: > 0 }:
                return simple.Keys.Select((k, i) => new CurveKeyInfo(i, k.Time, k.Value, simple.InterpMode, null, null, null)).ToArray();
            default:
                return Array.Empty<CurveKeyInfo>();
        }
    }
}

/// <summary>
/// A curve asset broken down into channels: 1 for <see cref="UCurveFloat"/>, X/Y/Z for
/// <see cref="UCurveVector"/>, R/G/B/A for <see cref="UCurveLinearColor"/>.
/// </summary>
public sealed class CurveAsset
{
    // Channel colors follow UE's curve editor convention.
    public static readonly SKColor FloatColor = new(0xF0, 0xA0, 0x4B);
    public static readonly SKColor RedColor = new(0xF0, 0x5A, 0x5A);
    public static readonly SKColor GreenColor = new(0x6C, 0xCB, 0x5F);
    public static readonly SKColor BlueColor = new(0x4F, 0x9B, 0xF5);
    public static readonly SKColor AlphaColor = new(0xC8, 0xC8, 0xC8);

    public string Name { get; }
    public CurveAssetKind Kind { get; }
    public IReadOnlyList<CurveChannel> Channels { get; }

    private CurveAsset(string name, CurveAssetKind kind, IReadOnlyList<CurveChannel> channels)
    {
        Name = name;
        Kind = kind;
        Channels = channels;
    }

    /// <summary>Builds an asset from already-made channels (tests, embedded curves).</summary>
    public static CurveAsset FromChannels(string name, CurveAssetKind kind, IReadOnlyList<CurveChannel> channels) => new(name, kind, channels);

    public static bool IsSupported(UObject obj) => obj is UCurveFloat or UCurveVector or UCurveLinearColor;

    public static bool TryCreate(UObject obj, out CurveAsset asset)
    {
        asset = obj switch
        {
            UCurveFloat => new CurveAsset(obj.Name, CurveAssetKind.Float,
            [
                new CurveChannel("Value", ReadCurve(obj, "FloatCurve", 0), FloatColor)
            ]),
            UCurveVector => new CurveAsset(obj.Name, CurveAssetKind.Vector,
            [
                new CurveChannel("X", ReadCurve(obj, "FloatCurves", 0), RedColor),
                new CurveChannel("Y", ReadCurve(obj, "FloatCurves", 1), GreenColor),
                new CurveChannel("Z", ReadCurve(obj, "FloatCurves", 2), BlueColor)
            ]),
            UCurveLinearColor => new CurveAsset(obj.Name, CurveAssetKind.LinearColor,
            [
                new CurveChannel("R", ReadCurve(obj, "FloatCurves", 0), RedColor),
                new CurveChannel("G", ReadCurve(obj, "FloatCurves", 1), GreenColor),
                new CurveChannel("B", ReadCurve(obj, "FloatCurves", 2), BlueColor),
                new CurveChannel("A", ReadCurve(obj, "FloatCurves", 3), AlphaColor)
            ]),
            _ => null
        };
        return asset != null;
    }

    /// <summary>
    /// Reads a curve straight from the property tags by name and array index.
    /// Channels left at their default value aren't serialized, so property order can't be trusted.
    /// </summary>
    private static FRealCurve ReadCurve(UObject obj, string propertyName, int arrayIndex)
    {
        foreach (var property in obj.Properties)
        {
            if (property.Name.Text != propertyName || property.ArrayIndex != arrayIndex)
                continue;

            switch (property.Tag?.GenericValue)
            {
                case FScriptStruct { StructType: FRealCurve curve }:
                    return curve;
                case FScriptStruct { StructType: FStructFallback fallback }:
                    return new FRichCurve(fallback);
            }
        }

        return new FRichCurve(); // not serialized = default (empty) curve
    }

    /// <summary>
    /// Unadjusted linear color at <paramref name="time"/> (alpha defaults to 1 when the A curve has no keys, like UE).
    /// </summary>
    public (float R, float G, float B, float A) EvalColor(double time)
    {
        if (Kind != CurveAssetKind.LinearColor) return (0, 0, 0, 1);
        var a = Channels[3].HasKeys ? Channels[3].Eval(time) : 1f;
        return (Channels[0].Eval(time), Channels[1].Eval(time), Channels[2].Eval(time), a);
    }
}
