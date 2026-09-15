using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Domain;

/// <summary>
///     The quarter arithmetic, copied from <c>MarchOfMurlocsHandler</c> so the two quarterly features
///     never disagree about the boundary minute (docs/design/seasons.md D36): a season is a calendar
///     quarter that ends at 23:59:59 UTC-5 on the last day of March, June, September or December.
///     Pure, so the roll's idempotency and the boundary minute are unit-tested without a clock or a table.
/// </summary>
internal static class SeasonCalendar
{
    /// <summary>The boundary offset March of Murlocs uses; a quarter turns over at midnight here.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    /// <summary>Phoenix 2's first season, where the backfill starts (D23).</summary>
    public static readonly SeasonId First = SeasonId.From(2026, 3);

    public static SeasonId QuarterAt(DateTimeOffset now)
    {
        var local = now.ToOffset(Offset);
        return SeasonId.From(local.Year, (local.Month - 1) / 3 + 1);
    }

    public static DateTimeOffset StartOf(SeasonId season)
    {
        return new DateTimeOffset(new DateTime(season.Year, (season.Quarter - 1) * 3 + 1, 1, 0, 0, 0), Offset);
    }

    public static DateTimeOffset EndOf(SeasonId season)
    {
        var month = season.Quarter * 3;
        return new DateTimeOffset(
            new DateTime(season.Year, month, DateTime.DaysInMonth(season.Year, month), 23, 59, 59), Offset);
    }

    public static SeasonId Next(SeasonId season)
    {
        return season.Quarter == 4 ? SeasonId.From(season.Year + 1, 1) : SeasonId.From(season.Year, season.Quarter + 1);
    }

    /// <summary>"Fall 2026" — MoM's season name, so the two features read the same on every surface.</summary>
    public static string NameOf(SeasonId season)
    {
        return $"{QuarterName(season.Quarter)} {season.Year}";
    }

    /// <summary>
    ///     Whether a season's window is behind us. There is no grace period (D13, owner 2026-09-14):
    ///     a season is over at its boundary and the next roll seals it, so exactly one season is ever
    ///     writable. You import before the season ends or the plays you did not import are lost —
    ///     a board that kept shifting for a week after the quarter closed is not worth explaining.
    /// </summary>
    public static bool HasEnded(SeasonId season, DateTimeOffset now)
    {
        return now > EndOf(season);
    }

    private static string QuarterName(int quarter)
    {
        return quarter switch
        {
            1 => "Winter",
            2 => "Spring",
            3 => "Summer",
            4 => "Fall",
            _ => throw new ArgumentOutOfRangeException(nameof(quarter), quarter, "Quarters run 1..4")
        };
    }
}
