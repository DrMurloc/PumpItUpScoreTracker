namespace ScoreTracker.Web.Services;

/// <summary>
///     The compact label for a rolled-up PUMBILITY ladder span ("[S] ADVANCED LV.6 → 9"): the
///     reached rung keeps only what differs from the first, so a same-band climb reads as its
///     numbers and a band-crossing climb keeps the second band's name ("[S] ADVANCED LV.9 →
///     EXPERT LV.2"). The trim backs up to a word boundary so "LV.1 → LV.12" can never mangle
///     into "1 → 2".
/// </summary>
public static class TitleSpans
{
    public static string Compact(string from, string to)
    {
        if (string.IsNullOrEmpty(from)) return to;
        if (string.IsNullOrEmpty(to) || from == to) return from;

        var common = 0;
        var max = Math.Min(from.Length, to.Length);
        while (common < max && from[common] == to[common]) common++;
        while (common > 0 && from[common - 1] != ' ' && from[common - 1] != '.') common--;

        var suffix = to[common..];
        return suffix.Length == 0 ? $"{from} → {to}" : $"{from} → {suffix}";
    }
}
