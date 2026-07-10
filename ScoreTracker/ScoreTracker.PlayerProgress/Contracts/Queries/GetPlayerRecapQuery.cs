using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     The persisted recap for a player, or null when none has been computed yet (or the
///     stored payload predates the current schema version).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerRecapQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix) : IQuery<PlayerRecap?>;
