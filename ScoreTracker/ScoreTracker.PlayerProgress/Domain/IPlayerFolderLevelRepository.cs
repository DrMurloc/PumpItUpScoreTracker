using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

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

    // The seasonal siblings (docs/design/seasons.md D12, D38): the season pass writes and reads the
    // same folder rows under the season's number, the folder code unchanged. Siblings rather than a
    // defaulted parameter — a Moq setup is an expression tree, and CS0854 forbids omitting an
    // optional argument in one.
    Task<IEnumerable<FolderLevelRecord>> GetFolderLevels(MixEnum mix, Guid userId, SeasonId season,
        CancellationToken cancellationToken);

    Task Save(Guid userId, IEnumerable<FolderLevelRecord> levels, DateTimeOffset asOf, SeasonId season,
        CancellationToken cancellationToken);
}
