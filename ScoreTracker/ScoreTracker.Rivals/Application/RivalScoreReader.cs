using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     Rival scores across a set of charts, merged from the two sources a rival can have
///     (docs/design/rivals.md §2.5). Every "what did my rivals get on this" surface goes through
///     here, so the ghost/live seam is solved once instead of at each of them.
///     <para>
///         Both sides are SET-BASED. Three hundred rivals across a session's worth of charts is
///         the shape this gets exercised in; a query per rival or per chart would put that burst
///         behind a page render.
///     </para>
/// </summary>
internal sealed class RivalScoreReader
{
    private readonly IMediator _mediator;
    private readonly IScoreReader _scores;

    public RivalScoreReader(IScoreReader scores, IMediator mediator)
    {
        _scores = scores;
        _mediator = mediator;
    }

    public async Task<RivalChartScores> Read(IReadOnlyList<RivalSubject> rivals, MixEnum mix,
        IReadOnlyCollection<Guid> chartIds, CancellationToken cancellationToken)
    {
        if (rivals.Count == 0 || chartIds.Count == 0) return RivalChartScores.Empty;

        var byChart = new Dictionary<Guid, List<RivalChartScore>>();
        await AddSiteScores(rivals, mix, chartIds, byChart, cancellationToken);
        // The official mirror covers the current generation only, so there are no board
        // placements to fold in on an older mix — a legacy comparison is site scores alone.
        var asOf = mix.UsesLegacyScoring()
            ? null
            : await AddOfficialScores(rivals, mix, chartIds, byChart, cancellationToken);

        return new RivalChartScores(asOf,
            byChart.ToDictionary(kv => kv.Key,
                kv => (IReadOnlyList<RivalChartScore>)kv.Value
                    .OrderByDescending(s => s.Score)
                    .ToArray()));
    }

    private async Task AddSiteScores(IReadOnlyList<RivalSubject> rivals, MixEnum mix,
        IReadOnlyCollection<Guid> chartIds, IDictionary<Guid, List<RivalChartScore>> byChart,
        CancellationToken cancellationToken)
    {
        var byUserId = rivals.Where(r => r.UserId != null).ToDictionary(r => r.UserId!.Value);
        if (byUserId.Count == 0) return;

        if (mix.UsesLegacyScoring())
        {
            // Legacy bests live in their own store, and their scores exceed a PhoenixScore in
            // three cases out of four — reading them through the Phoenix path returned nothing
            // at best and would have thrown at worst.
            var legacy = await _scores.GetPlayerLegacyScores(mix, byUserId.Keys, chartIds, cancellationToken);
            foreach (var score in legacy)
            {
                if (!byUserId.TryGetValue(score.UserId, out var legacyRival)) continue;
                Add(byChart, score.ChartId, new RivalChartScore(legacyRival.EdgeId, legacyRival.UserId,
                    legacyRival.Tag, legacyRival.DisplayName, legacyRival.Avatar, score.Score ?? 0,
                    null, score.IsBroken, RivalScoreSource.Site, score.LetterGrade));
            }

            return;
        }

        var scores = await _scores.GetPlayerScores(mix, byUserId.Keys, chartIds, cancellationToken);
        foreach (var score in scores)
        {
            if (!byUserId.TryGetValue(score.UserId, out var rival)) continue;
            Add(byChart, score.ChartId, new RivalChartScore(rival.EdgeId, rival.UserId, rival.Tag,
                rival.DisplayName, rival.Avatar, (int)score.Score, score.Plate, score.IsBroken,
                RivalScoreSource.Site));
        }
    }

    /// <summary>
    ///     Board-only rivals. Returns the snapshot instant so the caller can footnote it once per
    ///     board — these numbers are up to a week old and sit beside live ones.
    /// </summary>
    private async Task<DateTimeOffset?> AddOfficialScores(IReadOnlyList<RivalSubject> rivals, MixEnum mix,
        IReadOnlyCollection<Guid> chartIds, IDictionary<Guid, List<RivalChartScore>> byChart,
        CancellationToken cancellationToken)
    {
        var byTag = rivals.Where(r => r.IsGhost && r.Tag != null)
            .ToDictionary(r => r.Tag!, StringComparer.OrdinalIgnoreCase);
        if (byTag.Count == 0) return null;

        var official = await _mediator.Send(
            new GetOfficialScoresForTagsQuery(mix, byTag.Keys.ToArray(), chartIds), cancellationToken);

        foreach (var score in official.Scores)
        {
            if (!byTag.TryGetValue(score.Tag, out var rival)) continue;
            Add(byChart, score.ChartId, new RivalChartScore(rival.EdgeId, null, rival.Tag, rival.DisplayName,
                rival.Avatar, score.Score, null, false, RivalScoreSource.Official));
        }

        return official.AsOf;
    }

    private static void Add(IDictionary<Guid, List<RivalChartScore>> byChart, Guid chartId,
        RivalChartScore score)
    {
        if (!byChart.TryGetValue(chartId, out var list))
        {
            list = new List<RivalChartScore>();
            byChart[chartId] = list;
        }

        list.Add(score);
    }
}
