using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Domain;

/// <summary>
///     The season rows as the roll writes them (docs/design/seasons.md §6.1, §7). The rest of the
///     system reads them through the published <c>ISeasonReader</c>, which the same adapter serves.
/// </summary>
internal interface ISeasonRepository
{
    /// <summary>Every season, most recent first.</summary>
    Task<IReadOnlyList<SeasonRecord>> GetAll(CancellationToken cancellationToken);

    Task Add(SeasonRecord season, CancellationToken cancellationToken);

    /// <summary>Stamps <c>SealedAt</c> once (D13); a season already sealed keeps its first stamp.</summary>
    Task Seal(SeasonId season, DateTimeOffset sealedAt, CancellationToken cancellationToken);
}
