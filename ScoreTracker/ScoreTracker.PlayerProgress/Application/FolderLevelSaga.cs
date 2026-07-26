using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The folder standings' non-pipeline entry points: the read every surface goes through, and
///     the admin backfill that seeds players the import pipeline has not reached yet
///     (docs/design/folder-level-progression.md §4, §7.6). The pipeline write itself lives in
///     <see cref="HighlightCaptureSaga" />, where the charts and bests are already loaded.
/// </summary>
internal sealed class FolderLevelSaga : IConsumer<BackfillFolderLevelsCommand>,
    IRequestHandler<GetPlayerFolderLevelsQuery, IEnumerable<FolderLevelRecord>>
{
    // Both primary mixes, oldest first — Phoenix carries the audience, Phoenix 2 is cheap.
    private static readonly MixEnum[] BackfilledMixes = { MixEnum.Phoenix, MixEnum.Phoenix2 };

    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IPlayerFolderLevelRepository _folderLevels;
    private readonly ILogger<FolderLevelSaga> _logger;
    private readonly IScoreReader _scores;

    public FolderLevelSaga(IChartRepository charts, IScoreReader scores,
        IPlayerFolderLevelRepository folderLevels, IDateTimeOffsetAccessor dateTime,
        ILogger<FolderLevelSaga> logger)
    {
        _charts = charts;
        _scores = scores;
        _folderLevels = folderLevels;
        _dateTime = dateTime;
        _logger = logger;
    }

    /// <summary>
    ///     One player at a time, one mix at a time. Chunking is the point: the naive version of
    ///     this is a single aggregate across every user's whole history, which is exactly the
    ///     query shape that took production SQL down on 2026-07-10. A player who fails is logged
    ///     and skipped — the sweep must not abandon the remaining thousand.
    /// </summary>
    public async Task Consume(ConsumeContext<BackfillFolderLevelsCommand> context)
    {
        var cancellationToken = context.CancellationToken;
        var asOf = _dateTime.Now;

        foreach (var mix in BackfilledMixes)
        {
            var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToArray();
            if (charts.Length == 0)
            {
                _logger.LogInformation("Folder level backfill skipped {Mix} — no charts", mix);
                continue;
            }

            var userIds = await _scores.GetActiveUserIds(mix, DateTimeOffset.MinValue, cancellationToken);
            _logger.LogInformation("Folder level backfill starting for {Count} players on {Mix}",
                userIds.Count, mix);

            var done = 0;
            foreach (var userId in userIds)
                try
                {
                    var bests = await _scores.GetBestScores(mix, userId, cancellationToken);
                    var passed = FolderLevelCalculator.PassedScores(bests);
                    var levels = FolderLevelCalculator.Compute(mix, charts, passed)
                        // A folder the player has never touched carries no information a missing
                        // row does not, and storing it would write ~40 dead rows per player.
                        .Where(l => l.Played > 0)
                        .ToArray();
                    await _folderLevels.Save(userId, levels, asOf, cancellationToken);
                    done++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Folder level backfill failed for {UserId} on {Mix}", userId, mix);
                }

            _logger.LogInformation("Folder level backfill finished: {Done} of {Count} on {Mix}",
                done, userIds.Count, mix);
        }
    }

    public async Task<IEnumerable<FolderLevelRecord>> Handle(GetPlayerFolderLevelsQuery request,
        CancellationToken cancellationToken) =>
        await _folderLevels.GetFolderLevels(request.Mix, request.UserId, cancellationToken);
}
