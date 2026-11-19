using System;
using System.Collections.Generic;
using System.Globalization;

namespace FModel.Viewers.Core;

/// <summary>
/// "Nice number" tick generation for chart axes (1/2/5 × 10^n steps).
/// </summary>
public static class AxisTicks
{
    public readonly record struct Ticks(double Start, double Step, int Decimals, int Count)
    {
        public IEnumerable<double> Values()
        {
            for (var i = 0; i < Count; i++)
                yield return Start + i * Step;
        }

        public string Format(double value)
        {
            if (Math.Abs(value) < Step * 1e-6) value = 0; // avoid "-0" and float noise
            return value.ToString(Decimals > 0 ? "F" + Decimals : "0", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Computes ticks covering [min, max] with roughly <paramref name="targetCount"/> intervals.
    /// </summary>
    public static Ticks Compute(double min, double max, int targetCount)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || targetCount < 1)
            return new Ticks(0, 1, 0, 0);
        if (max < min) (min, max) = (max, min);

        var range = max - min;
        if (range <= 0) range = Math.Max(Math.Abs(min), 1) * 1e-3;

        var step = NiceStep(range / targetCount);
        var start = Math.Ceiling(min / step) * step;
        var count = (int) Math.Floor((max - start) / step + 1e-9) + 1;
        count = Math.Clamp(count, 0, 1000);

        var decimals = Math.Max(0, (int) -Math.Floor(Math.Log10(step) + 1e-9));
        // Steps like 0.25 need more decimals than their order of magnitude suggests.
        if (Math.Abs(Math.Round(step * Math.Pow(10, decimals)) - step * Math.Pow(10, decimals)) > 1e-6)
            decimals++;

        return new Ticks(start, step, Math.Min(decimals, 10), count);
    }

    public static double NiceStep(double rawStep)
    {
        if (rawStep <= 0 || !double.IsFinite(rawStep)) return 1;
        var exponent = Math.Floor(Math.Log10(rawStep));
        var magnitude = Math.Pow(10, exponent);
        var fraction = rawStep / magnitude;

        var nice = fraction switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 2.5 => 2.5,
            <= 5 => 5,
            _ => 10
        };
        return nice * magnitude;
    }
}
