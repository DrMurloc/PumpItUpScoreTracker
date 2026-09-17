using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The PUMBILITY presence census and its read (docs/design/chart-presence-graph.md).
///     <para>
///         The census counts the players standing on the gem ladder the way the Breakdown card's
///         title cohort does: PIU Scores accounts by their stored PUMBILITY, and the official
///         ranking's own players by the number it publishes, their fifties rebuilt from the chart
///         rankings. The read adds the viewer, whose title and top 50 are theirs today rather than
///         the census's.
///     </para>
/// </summary>
internal sealed class ChartPresenceSaga :
    IConsumer<RebuildChartPresenceCommand>,
    IRequestHandler<GetChartPumbilityPresenceQuery, ChartPumbilityPresenceRecord?>
{
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILogger<ChartPresenceSaga> _logger;
    private readonly IOfficialPlacementReader _official;
    private readonly IChartPresenceRepository _repository;
    private readonly IScoreReader _scores;
    private readonly IPlayerStatsReader _stats;

    public ChartPresenceSaga(IChartPresenceRepository repository, IChartRepository charts,
        IPlayerStatsReader stats, IScoreReader scores, IOfficialPlacementReader official,
        IDateTimeOffsetAccessor clock, ILogger<ChartPresenceSaga> logger)
    {
        _repository = repository;
        _charts = charts;
        _stats = stats;
        _scores = scores;
        _official = official;
        _clock = clock;
        _logger = logger;
    }

    private static readonly ChartType[] PoolTypes = { ChartType.Single, ChartType.Double };

    public async Task Consume(ConsumeContext<RebuildChartPresenceCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var charts = await ScoreableCharts(mix, scoring, cancellationToken);
        if (charts.Count == 0)
        {
            _logger.LogInformation("Presence census skipped for {Mix}: the mix has no catalog", mix);
            return;
        }

        var ladderFloor = PumbilityBand.Gems()[0].Floor;

        var accounts = (await _stats.GetPlayersInPoolBand(mix, PumbilityPool.Total, ladderFloor, null,
            cancellationToken)).ToArray();
        var standing = (await _stats.GetStats(mix, accounts, cancellationToken))
            .ToDictionary(s => s.UserId, s => s.SkillRating);
        var records = new List<UserPhoenixScore>();
        foreach (var type in PoolTypes)
            records.AddRange(await _scores.GetPlayerScoresInLevelRange(mix, accounts, type,
                PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, cancellationToken));

        var (boardScores, published) = await BoardPlayers(mix, ladderFloor, cancellationToken);

        var voices = accounts.Where(standing.ContainsKey).Select(PeerVoice.Account)
            .Concat(published.Keys).ToHashSet();
        var fifties = PumbilityPeerPools.Fifties(records, voices, charts, scoring, boardScores);
        var counted = voices.Select(voice => voice.UserId is { } userId
                ? new PresenceVoice(voice, standing[userId], false, fifties[voice])
                : new PresenceVoice(voice, published[voice], true, fifties[voice]))
            .ToArray();

        var census = ChartPresenceCensus.Take(counted, charts,
            await _official.GetChartsWithBoards(mix, cancellationToken));
        if (census.Columns.All(c => c.Players == 0))
        {
            _logger.LogInformation("Presence census skipped for {Mix}: nobody stands on the ladder", mix);
            return;
        }

        await _repository.Replace(mix, census, _clock.Now, cancellationToken);
        _logger.LogInformation("Presence census for {Mix}: {Players} players, {Columns} columns, {Rows} rows",
            mix, counted.Length, census.Columns.Count, census.Rows.Count);
    }

    public async Task<ChartPumbilityPresenceRecord?> Handle(GetChartPumbilityPresenceQuery request,
        CancellationToken cancellationToken)
    {
        var columns = await _repository.GetColumns(request.Mix, cancellationToken);
        if (columns.Rows.Count == 0 || columns.ComputedAt is not { } computedAt) return null;
        var rows = await _repository.GetRows(request.Mix, request.ChartId, cancellationToken);
        if (rows.Count == 0) return null;

        double? pumbility = null;
        double? spot = null;
        if (request.ViewerId is { } viewer)
        {
            pumbility = (await _stats.GetStats(request.Mix, viewer, cancellationToken)).SkillRating;
            spot = await SpotInOwnFifty(request.Mix, viewer, request.ChartId, cancellationToken);
        }

        return ChartPresenceReading.Read(columns.Rows, rows, computedAt, pumbility, spot,
            await CrowdingOf(request.Mix, request.ChartId, cancellationToken));
    }

    /// <summary>
    ///     The chart's official ranking in the latest sweep when it is crowded enough to hide the players who
    ///     hold the chart (docs/design/chart-presence-graph.md §8), or null.
    /// </summary>
    private async Task<PumbilityPresenceCrowding?> CrowdingOf(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken)
    {
        var rankings = await _official.GetChartRankings(mix, cancellationToken);
        if (!rankings.TryGetValue(chartId, out var ranking)) return null;
        var chart = await _charts.GetChart(mix, chartId, cancellationToken);
        return CrowdedRanking.HidesHolders(chart, ranking) ? new PumbilityPresenceCrowding(ranking.LowestScore) : null;
    }

    /// <summary>
    ///     The official ranking's players on the ladder, placed by the number it publishes, and their
    ///     best score per chart. A player who owns more than one ranking row counts once.
    /// </summary>
    private async Task<(IReadOnlyList<BoardPeerScore> Scores, IReadOnlyDictionary<PeerVoice, double> Published)>
        BoardPlayers(MixEnum mix, double ladderFloor, CancellationToken cancellationToken)
    {
        var band = await _official.GetBoardBand(mix, PumbilityPool.Total, ladderFloor, null, cancellationToken);
        var voiceOf = new Dictionary<int, PeerVoice>();
        var published = new Dictionary<PeerVoice, double>();
        foreach (var player in band?.Peers ?? Array.Empty<BoardPeerReading>())
        {
            var voice = PeerVoice.FromBoard(player.BoardPlayerIds[0], player.Tag);
            published[voice] = player.Pool;
            foreach (var id in player.BoardPlayerIds) voiceOf[id] = voice;
        }

        if (voiceOf.Count == 0) return (Array.Empty<BoardPeerScore>(), published);

        var rows = new List<BoardScoreReading>();
        foreach (var type in PoolTypes)
            rows.AddRange(await _official.GetBoardScores(mix, type, voiceOf.Keys.ToArray(),
                PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, cancellationToken));

        var scores = rows.Where(r => voiceOf.ContainsKey(r.BoardPlayerId))
            .GroupBy(r => (Voice: voiceOf[r.BoardPlayerId], r.ChartId))
            .Select(g => new BoardPeerScore(g.Key.Voice, g.Key.ChartId, g.Max(r => r.Score)))
            .ToArray();
        return (scores, published);
    }

    /// <summary>The chart's spot in the viewer's own top 50, built by the rule the census builds everyone's by.</summary>
    private async Task<double?> SpotInOwnFifty(MixEnum mix, Guid userId, Guid chartId,
        CancellationToken cancellationToken)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var charts = await ScoreableCharts(mix, scoring, cancellationToken);
        if (!charts.ContainsKey(chartId)) return null;

        var voice = PeerVoice.Account(userId);
        var records = (await _scores.GetBestScores(mix, userId, cancellationToken))
            .Where(r => r.Score != null && !r.IsBroken && charts.ContainsKey(r.ChartId))
            .Select(r => new UserPhoenixScore(userId, r.ChartId, Name.From("You"), r.Score!.Value, r.Plate,
                r.IsBroken));
        var fifty = PumbilityPeerPools.Fifties(records, new HashSet<PeerVoice> { voice }, charts, scoring)[voice];
        return ChartPresenceCensus.Spots(fifty).TryGetValue(chartId, out var spot) ? spot : null;
    }

    /// <summary>
    ///     The singles and doubles charts the mix's PUMBILITY can price above zero — the only charts a
    ///     top 50 can hold, and the only ones the census writes rows for.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Chart>> ScoreableCharts(MixEnum mix, ScoringConfiguration scoring,
        CancellationToken cancellationToken)
    {
        return (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .Where(c => c.Type is ChartType.Single or ChartType.Double)
            .Where(c => scoring.GetScore(c, PhoenixScore.From(1_000_000), PhoenixPlate.PerfectGame, false) > 0)
            .ToDictionary(c => c.Id);
    }
}
