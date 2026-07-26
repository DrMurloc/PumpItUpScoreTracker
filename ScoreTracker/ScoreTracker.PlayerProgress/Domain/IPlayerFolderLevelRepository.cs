using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     The stored folder standings (docs/design/folder-level-progression.md §4). Persisted rather
///     than derived on read because a milestone needs the previous tier and grade to diff against,
///     and nothing in the score journal carries them.
/// </summary>
internal interface IPlayerFolderLevelRepository
{
    Task<IEnumerable<FolderLevelRecord>> GetFolderLevels(MixEnum mix, Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Upserts each folder's row, stamping <paramref name="asOf" /> on the ones that move.</summary>
    Task Save(Guid userId, IEnumerable<FolderLevelRecord> levels, DateTimeOffset asOf,
        CancellationToken cancellationToken);
}
