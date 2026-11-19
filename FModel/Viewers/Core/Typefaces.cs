using System.Collections.Concurrent;
using SkiaSharp;

namespace FModel.Viewers.Core;

/// <summary>Process-wide typeface cache; typefaces are never disposed.</summary>
public static class Typefaces
{
    private static readonly ConcurrentDictionary<string, SKTypeface> _cache = new();

    public static SKTypeface Get(string family) =>
        _cache.GetOrAdd(family ?? string.Empty, f => (string.IsNullOrEmpty(f) ? null : SKTypeface.FromFamilyName(f)) ?? SKTypeface.Default);
}
