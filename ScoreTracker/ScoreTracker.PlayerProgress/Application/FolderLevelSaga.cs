using MediatR;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The folder standings' read entry point, which every surface goes through
///     (docs/design/folder-level-progression.md §4). The write lives in
///     <see cref="HighlightCaptureSaga" />, where the charts and bests are already loaded.
/// </summary>
internal sealed class FolderLevelSaga :
    IRequestHandler<GetPlayerFolderLevelsQuery, IEnumerable<FolderLevelRecord>>
{
    private readonly IPlayerFolderLevelRepository _folderLevels;

    public FolderLevelSaga(IPlayerFolderLevelRepository folderLevels)
    {
        _folderLevels = folderLevels;
    }

    public async Task<IEnumerable<FolderLevelRecord>> Handle(GetPlayerFolderLevelsQuery request,
        CancellationToken cancellationToken) =>
        await _folderLevels.GetFolderLevels(request.Mix, request.UserId, cancellationToken);
}
