using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

internal interface IPlayerSeasonRecapRepository
{
    Task SaveRecap(Guid userId, MixEnum mix, PlayerRecap recap, CancellationToken cancellationToken);

    /// <summary>
    ///     Null when no recap exists — or when the stored payload predates
    ///     PlayerRecap.CurrentSchemaVersion, which reads as "not computed yet"
    ///     until an admin refires the sweep.
    /// </summary>
    Task<PlayerRecap?> GetRecap(Guid userId, MixEnum mix, CancellationToken cancellationToken);
}
