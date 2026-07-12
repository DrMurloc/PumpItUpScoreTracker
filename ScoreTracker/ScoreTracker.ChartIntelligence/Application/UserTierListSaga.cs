using MassTransit;
using MediatR;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Materializes each player's relative tier list into UserTierListEntry rows,
///     event-driven off score imports (tier-lists overhaul design doc §6, Tier 2). The
///     similar-players aggregation reads these rows as one set-based query instead of
///     firing GetMyRelativeTierListQuery once per neighboring player. Idempotent —
///     replaying an event recomputes the same folders to the same rows, per the
///     in-memory-transport rules.
/// </summary>
internal sealed class UserTierListSaga : IConsumer<PlayerScoresUpdatedEvent>,
    IConsumer<BackfillUserTierListsCommand>
{
    // Keeps the one-time backfill gentle on the shared database tier.
    private static readonly TimeSpan BackfillDelayPerUser = TimeSpan.FromMilliseconds(100);

    private readonly IChartRepository _charts;
    private readonly IMediator _mediator;
    private readonly IScoreReader _scores;
    private readonly IUserTierListRepository _userTierLists;

    public UserTierListSaga(IChartRepository charts, IMediator mediator, IScoreReader scores,
        IUserTierListRepository userTierLists)
    {
        _charts = charts;
        _mediator = mediator;
        _scores = scores;
        _userTierLists = userTierLists;
    }

    public async Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
    {
        var message = context.Message;
        var changedIds = message.Changes.Select(c => c.ChartId).Distinct().ToArray();
        if (!changedIds.Any()) return;

        var changedCharts = await _charts.GetCharts(message.Mix, chartIds: changedIds,
            cancellationToken: context.CancellationToken);
        foreach (var (type, level) in Folders(changedCharts))
            await MaterializeFolder(message.Mix, message.UserId, type, level, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<BackfillUserTierListsCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var allCharts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var userIds = await _scores.GetActiveUserIds(mix, DateTimeOffset.MinValue, cancellationToken);
        foreach (var userId in userIds)
        {
            var scoredCharts = (await _scores.GetBestScores(mix, userId, cancellationToken))
                .Where(s => allCharts.ContainsKey(s.ChartId))
                .Select(s => allCharts[s.ChartId]);
            foreach (var (type, level) in Folders(scoredCharts))
                await MaterializeFolder(mix, userId, type, level, cancellationToken);

            await Task.Delay(BackfillDelayPerUser, cancellationToken);
        }
    }

    // A folder is (type, level); performance charts live in their base type's folder and
    // CoOp has no relative tier list (personalization is disabled for CoOp).
    private static IEnumerable<(ChartType Type, DifficultyLevel Level)> Folders(IEnumerable<Chart> charts)
    {
        return charts.Select(c => (Type: Normalize(c.Type), c.Level))
            .Where(f => f.Type is ChartType.Single or ChartType.Double)
            .Distinct();
    }

    private static ChartType Normalize(ChartType type)
    {
        return type switch
        {
            ChartType.SinglePerformance => ChartType.Single,
            ChartType.DoublePerformance => ChartType.Double,
            _ => type
        };
    }

    private async Task MaterializeFolder(MixEnum mix, Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken)
    {
        var entries = await _mediator.Send(new GetMyRelativeTierListQuery(chartType, level, userId, mix),
            cancellationToken);
        // Same read the query handler itself scopes the folder with — the stale-row
        // cleanup must agree with it about which charts are "the folder".
        var folderChartIds = (await _charts.GetCharts(mix, level, chartType, cancellationToken: cancellationToken))
            .Select(c => c.Id).ToArray();
        await _userTierLists.SaveUserFolder(mix, userId, folderChartIds, entries, cancellationToken);
    }
}
