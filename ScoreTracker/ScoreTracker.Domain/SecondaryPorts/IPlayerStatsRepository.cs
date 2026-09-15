using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPlayerStatsRepository
{
    Task SaveStats(MixEnum mix, Guid userId, PlayerStatsRecord newStats, CancellationToken cancellationToken);
    Task<PlayerStatsRecord> GetStats(MixEnum mix, Guid userId, CancellationToken cancellationToken);

    Task<IEnumerable<PlayerStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> userIds,
        CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetPlayersByCompetitiveRange(MixEnum mix, ChartType? chartType, double competitiveLevel,
        double range, CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetUserIdsWithStats(MixEnum mix, CancellationToken cancellationToken);

    Task DeleteStats(MixEnum mix, Guid userId, CancellationToken cancellationToken);

    // ---- The seasonal siblings (docs/design/seasons.md D12, §12.3) ----
    //
    // The row a season pass writes and reads is the same shape under the season's number; the
    // all-time methods above keep answering from the AllTime query filter. Siblings rather than a
    // defaulted parameter, because a Moq setup is an expression tree and CS0854 forbids omitting an
    // optional argument in one. The peer lookups (competitive range, pool of type, pool band) have no
    // sibling: peers are all-time by decision (D6). DeleteStats crosses seasons in place — a wipe has
    // no all-time-only form.

    Task SaveStats(MixEnum mix, Guid userId, PlayerStatsRecord newStats, SeasonId season,
        CancellationToken cancellationToken);

    Task<PlayerStatsRecord> GetStats(MixEnum mix, Guid userId, SeasonId season, CancellationToken cancellationToken);

    Task<IEnumerable<PlayerStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> userIds, SeasonId season,
        CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetUserIdsWithStats(MixEnum mix, SeasonId season, CancellationToken cancellationToken);
}