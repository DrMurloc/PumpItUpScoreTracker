using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Domain;

/// <summary>
///     The quarter arithmetic, copied from <c>MarchOfMurlocsHandler</c> so the two quarterly features
///     never disagree about the boundary minute (docs/design/seasons.md D36): a season is a calendar
///     quarter that ends at 23:59:59 UTC-5 on the last day of March, June, September or December.
///     Pure, so the roll's idempotency and the grace week are unit-tested without a clock or a table.
/// </summary>
internal static class SeasonCalendar
{
    /// <summary>The boundary offset March of Murlocs uses; a quarter turns over at midnight here.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    /// <summary>
    ///     D13: the roll seals a season seven days after its boundary; until then an in-window play
    ///     still lands on the ended season.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromDays(7);

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

    public static bool IsPastGrace(SeasonId season, DateTimeOffset now)
    {
        return now >= EndOf(season) + Grace;
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
