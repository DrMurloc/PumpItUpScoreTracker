using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     The Seasons vertical's published read contract (docs/design/seasons.md D35): which seasons
///     exist, which one holds a given play time, and whether it is sealed. Every vertical that
///     writes a season row — the Ledger's writer, Progression's season pass — asks this and never
///     the Seasons tables, so Seasons references nothing back. One row per quarter, so the whole list
///     is small enough to read once per write and reason over in memory.
/// </summary>
public interface ISeasonReader
{
    /// <summary>Every season the roll has opened, most recent first. Empty until the first roll.</summary>
    Task<IReadOnlyList<SeasonRecord>> GetSeasons(CancellationToken cancellationToken);

    /// <summary>
    ///     The season whose window holds <paramref name="at" />, sealed or not; null when no season
    ///     covers it — before the first roll, or a play time older than Summer 2026.
    /// </summary>
    Task<SeasonRecord?> GetSeasonAt(DateTimeOffset at, CancellationToken cancellationToken);
}
