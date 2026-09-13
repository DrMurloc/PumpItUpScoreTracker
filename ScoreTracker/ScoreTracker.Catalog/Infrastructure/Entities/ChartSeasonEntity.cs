namespace ScoreTracker.Catalog.Infrastructure.Entities;

/// <summary>
///     A chart's season rating where it differs from the printed level: one row per (season, mix,
///     chart), season first, and no row at all while a chart sits at printed
///     (docs/design/seasons.md D33, §6.3). ChartMix is untouched by seasons; this table is the
///     overlay a seasonal read lays over ChartMix.Level. Written by the season roll (slice 4) and
///     the admin pin (slice 2b) through a Catalog command, never by a join from another vertical.
/// </summary>
internal sealed class ChartSeasonEntity
{
    public short SeasonId { get; set; }

    public Guid MixId { get; set; }

    public Guid ChartId { get; set; }

    /// <summary>The season rating, 1–29.</summary>
    public int Level { get; set; }

    /// <summary>ChartMix.Level at the roll, so the marker survives a later re-level of the chart.</summary>
    public int PrintedLevel { get; set; }

    /// <summary>−1, 0 or +1: how this roll moved the chart from the previous season's rating.</summary>
    public short MovedThisRoll { get; set; }

    /// <summary>
    ///     The weighted hold that ranked the chart in its folder at the roll; null when the rating
    ///     was pinned by hand.
    /// </summary>
    public double? HoldWeight { get; set; }

    public int? HoldRank { get; set; }

    public int? LivePlayers { get; set; }

    /// <summary>Set by the admin console's pin; the next roll clears it.</summary>
    public bool IsPinned { get; set; }
}
