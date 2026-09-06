namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>
///     A session cut into twenty-minute sections by when each chart started (D25): the rows of
///     the share image, "tier-listed by when the chart landed in the session" in the owner's
///     words. Six sections cover the window — a chart may start as late as 1:45, which is the
///     "100 min +" row — and a section with nothing in it is not a row.
/// </summary>
public static class MoMSections
{
    public const int MinutesPerSection = 20;
    public const int LastSection = 5;

    [ExcludeFromCodeCoverage]
    public sealed record MoMSection(int Index, int FromMinute, int? ToMinute, IReadOnlyList<MoMTimedChart> Charts);

    public static int Index(TimeSpan startsAt)
    {
        return Math.Min(LastSection, Math.Max(0, (int)Math.Floor(startsAt.TotalMinutes / MinutesPerSection)));
    }

    public static IReadOnlyList<MoMSection> Group(IReadOnlyList<MoMTimedChart> charts)
    {
        return charts
            .GroupBy(c => Index(c.StartsAt))
            .OrderBy(g => g.Key)
            .Select(g => new MoMSection(g.Key, g.Key * MinutesPerSection,
                g.Key == LastSection ? null : g.Key * MinutesPerSection + MinutesPerSection,
                g.OrderBy(c => c.StartsAt).ToArray()))
            .ToArray();
    }
}
