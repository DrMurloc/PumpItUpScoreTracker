using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The bucket-cached cohort machinery behind score-quality percentiles, extracted from
///     <see cref="ScoreQualitySaga" /> so bus-side consumers (the recap saga) can rank an
///     explicit player instead of the current web user. Cache keys keep their historical
///     "ScoreQualitySaga__" prefixes so both callers share one set of live entries — the
///     half-level bucketing and per-chart caching are what keep cohort reads off the
///     ledger (the 2026-07-10 incident fix); do not bypass this class for cohort scores.
/// </summary>
internal sealed class CohortScoreProvider
{
    private readonly IMemoryCache _cache;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;

    public CohortScoreProvider(IPlayerStatsReader playerStats, IScoreReader scores, IMemoryCache cache)
    {
        _playerStats = playerStats;
        _scores = scores;
        _cache = cache;
    }

    /// <summary>
    ///     Half-level buckets let players of similar strength share cached cohorts and
    ///     cohort scores; exact-level keys made every cache entry per-user, so each visitor
    ///     paid for their own copy of the same near-identical ledger query.
    /// </summary>
    public static double Bucket(double competitiveLevel)
    {
        return Math.Round(competitiveLevel * 2, MidpointRounding.AwayFromZero) / 2.0;
    }

    public async Task<IEnumerable<Guid>> GetComparablePlayers(MixEnum mix, ChartType chartType, double bucket,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            $"{nameof(ScoreQualitySaga)}__GetComparablePlayers__{mix}__{bucket}__{chartType}",
            async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return (await _playerStats.GetPlayersByCompetitiveRange(mix, chartType, bucket,
                    .5, cancellationToken)).ToArray().AsEnumerable();
            }) ?? Array.Empty<Guid>();
    }

    // Cohort score distributions are cached per chart (not per requested chart set) so
    // overlapping pages — home recommendations, uploads, breakdowns — hit the same
    // entries, and only genuinely unseen charts reach the ledger.
    public async Task<IReadOnlyDictionary<Guid, PhoenixScore[]>> GetCohortScoresByChart(MixEnum mix,
        ChartType chartType, double bucket, ISet<Guid> chartIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, PhoenixScore[]>();
        var missing = new List<Guid>();
        foreach (var chartId in chartIds)
            if (_cache.TryGetValue(CohortScoresKey(mix, chartType, bucket, chartId),
                    out PhoenixScore[]? cached) && cached != null)
            {
                if (cached.Length > 0) result[chartId] = cached;
            }
            else
            {
                missing.Add(chartId);
            }

        if (!missing.Any()) return result;

        var players = await GetComparablePlayers(mix, chartType, bucket, cancellationToken);
        var fetched = (await _scores.GetPlayerScores(mix, players, missing, cancellationToken))
            .GroupBy(c => c.ChartId)
            .ToDictionary(g => g.Key,
                g => g.OrderBy(s => s.Score).Select(s => s.Score).ToArray());
        foreach (var chartId in missing)
        {
            var scores = fetched.TryGetValue(chartId, out var chartScores)
                ? chartScores
                : Array.Empty<PhoenixScore>();
            // Charts nobody in the cohort has played are cached as empty so they don't
            // re-trigger the ledger query on every page load.
            _cache.Set(CohortScoresKey(mix, chartType, bucket, chartId), scores, TimeSpan.FromHours(1));
            if (scores.Length > 0) result[chartId] = scores;
        }

        return result;
    }

    private static string CohortScoresKey(MixEnum mix, ChartType chartType, double bucket, Guid chartId)
    {
        return $"{nameof(ScoreQualitySaga)}__CohortScores__{mix}__{chartType}__{bucket}__{chartId}";
    }
}
