using System.Globalization;

namespace DmCompanion.Screens.Services;

/// <summary>Tramo de un fichero: primer byte y número de bytes.</summary>
public readonly record struct ByteRange(long Start, long Length)
{
    public long End => Start + Length - 1;
}

public enum RangeParse { None, Ok, Unsatisfiable }

public static class ByteRanges
{
    private const string Unit = "bytes=";

    /// <summary>Interpreta "bytes=a-b", "bytes=a-" y "bytes=-n" (un solo rango). Varios rangos o sintaxis rara → None (se sirve entero).</summary>
    public static RangeParse TryParse(string? header, long totalLength, out ByteRange range)
    {
        range = default;
        if (header is null || !header.StartsWith(Unit, StringComparison.OrdinalIgnoreCase)) return RangeParse.None;
        var spec = header[Unit.Length..].Trim();
        var dash = spec.IndexOf('-');
        if (dash < 0 || spec.Contains(',')) return RangeParse.None;

        var first = spec[..dash].Trim();
        var last = spec[(dash + 1)..].Trim();
        if (first.Length == 0) return ParseSuffix(last, totalLength, out range);
        if (!TryNumber(first, out var start)) return RangeParse.None;

        long end;
        if (last.Length == 0) end = long.MaxValue;
        else if (!TryNumber(last, out end) || end < start) return RangeParse.None;

        if (start >= totalLength) return RangeParse.Unsatisfiable;
        range = new ByteRange(start, Math.Min(end, totalLength - 1) - start + 1);
        return RangeParse.Ok;
    }

    private static RangeParse ParseSuffix(string digits, long totalLength, out ByteRange range)
    {
        range = default;
        if (!TryNumber(digits, out var suffix) || suffix == 0) return RangeParse.None;
        if (totalLength == 0) return RangeParse.Unsatisfiable;
        var length = Math.Min(suffix, totalLength);
        range = new ByteRange(totalLength - length, length);
        return RangeParse.Ok;
    }

    private static bool TryNumber(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
