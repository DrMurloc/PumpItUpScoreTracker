using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     A player's stored folder standings for a mix — completion percent and folder grade per
///     (chart type, level). Returns only folders with a stored row: a player who has never
///     imported, or one the backfill has not reached, reads empty rather than as a wall of zeros.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerFolderLevelsQuery
    (Guid UserId, MixEnum Mix = MixEnum.Phoenix) : IQuery<IEnumerable<FolderLevelRecord>>
{
}
