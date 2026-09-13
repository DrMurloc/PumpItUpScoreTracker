namespace ScoreTracker.Seasons.Infrastructure.Entities;

/// <summary>
///     One quarterly season (docs/design/seasons.md §6.1). Keyed by the calendar number the whole
///     schema uses as its SeasonId — 20264 is Fall 2026 — so the key is the quarter itself and
///     MoM's anti-runaway unique index over (year, quarter) is the primary key here. SealedAt is
///     the seal (D13): set by the roll seven days after the boundary, after which nothing writes a
///     row carrying this season's number. There are no archive tables (D14); a sealed season's
///     rows stay where they are.
/// </summary>
internal sealed class SeasonEntity
{
    public short Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public DateTimeOffset? SealedAt { get; set; }

    /// <summary>False for every season running at launch (D3): its ratings are flat.</summary>
    public bool IsBalanced { get; set; }
}
