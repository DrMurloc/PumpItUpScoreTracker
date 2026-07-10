using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Weekly Challenge's published bulk-history read (ADR-001 D3 "pull"): the whole
///     archived placing table for a mix, added for the season-recap sweep which re-ranks
///     within-range entrants and hunts giant-slayer moments across every week at once.
/// </summary>
public interface IWeeklyPlacingReader
{
    Task<IEnumerable<WeeklyPlacingRow>> GetAllPlacings(MixEnum mix, CancellationToken cancellationToken = default);
}
