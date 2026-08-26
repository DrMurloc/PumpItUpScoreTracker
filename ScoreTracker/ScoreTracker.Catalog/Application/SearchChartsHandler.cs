using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The /Charts SRP search (docs/design/charts-srp.md §3). Charts come from the mix's
///     cached dictionary; community facets ride a per-mix bundle cached until just after the
///     nightly analytics chain, split by scoring family — Pass/Score Difficulty and Scoring
///     Level for Phoenix-family mixes, Community Vote for XX and older. Vote data never
///     reaches a modern-mix facet or ordering, tiebreaks included.
///     Searching is single-mix by design: a cross-mix view needs its own model (identity
///     grouping, per-appearance levels, incomparable scoring families) and is deferred to
///     its own design pass rather than bolted on here.
/// </summary>
internal sealed class SearchChartsHandler : IRequestHandler<SearchChartsQuery, ChartSearchResultPage>
{
    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IChartSkillMetricRepository _metrics;
    private readonly IChartDifficultyRatingRepository _ratings;
    private readonly IScoreReader _scores;
    private readonly IChartScoringLevelRepository _scoringLevels;
    private readonly ITierListRepository _tierLists;

    public SearchChartsHandler(IChartRepository charts, IChartSkillMetricRepository metrics,
        ITierListRepository tierLists, IChartScoringLevelRepository scoringLevels,
        IChartDifficultyRatingRepository ratings, IScoreReader scores, IMemoryCache cache,
        IDateTimeOffsetAccessor clock)
    {
        _charts = charts;
        _metrics = metrics;
        _tierLists = tierLists;
        _scoringLevels = scoringLevels;
        _ratings = ratings;
        _scores = scores;
        _cache = cache;
        _clock = clock;
    }

    public async Task<ChartSearchResultPage> Handle(SearchChartsQuery request, CancellationToken cancellationToken)
    {
        var charts = await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken);
        var metricsByChart = await _metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken);
        var bundle = await GetCommunityBundle(request.Mix, cancellationToken);

        var projected = charts.Select(c => Project(c, metricsByChart, bundle)).ToList();

        IReadOnlyDictionary<Guid, MyRecord>? myRecords = null;
        if (request.UserId != null)
        {
            myRecords = await LoadMyRecords(request.Mix, request.UserId.Value, cancellationToken);
            var records = myRecords;
            projected = projected.Select(r => r with { My = BuildMyState(r, records) }).ToList();
        }

        var rows = Filter(projected, request, myRecords).ToList();

        var sorted = Sort(rows, request).ToList();
        var total = sorted.Count;
        var page = request.Page == null
            ? sorted
            : sorted.Skip((Math.Max(1, request.Page.Value) - 1) * request.PageSize).Take(request.PageSize).ToList();

        return new ChartSearchResultPage(page, total,
            request.IncludeFacetCounts ? CountFacets(projected, request, myRecords) : null);
    }

    private static IEnumerable<ChartSearchResult> Filter(IEnumerable<ChartSearchResult> rows,
        SearchChartsQuery q, IReadOnlyDictionary<Guid, MyRecord>? myRecords)
    {
        return rows.Where(r => MatchesContent(r, q) && MatchesCommunity(r, q)
                               && (myRecords == null || MatchesUser(r, q, myRecords)));
    }

    /// <summary>
    ///     Per-facet distributions for the drawer. Each facet counts with its OWN filter
    ///     lifted, so the numbers answer "what would I get if I picked this instead" —
    ///     counted against the filtered set they would all read 0 the moment you chose one
    ///     value, which looks like a broken page rather than a live facet.
    /// </summary>
    private static ChartSearchFacetCounts CountFacets(IReadOnlyList<ChartSearchResult> projected,
        SearchChartsQuery q, IReadOnlyDictionary<Guid, MyRecord>? myRecords)
    {
        IEnumerable<ChartSearchResult> Without(SearchChartsQuery lifted) =>
            Filter(projected, lifted, myRecords);

        return new ChartSearchFacetCounts(
            Without(q with { Types = null }).GroupBy(r => r.Chart.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            Without(q with { SongTypes = null }).GroupBy(r => r.Chart.Song.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            Without(q with { Badges = null }).SelectMany(r => r.Badges.Select(b => b.Key))
                .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
            CountCategories(Without(q with { PassDifficulty = null }), r => r.PassDifficulty),
            CountCategories(Without(q with { ScoreDifficulty = null }), r => r.ScoreDifficulty),
            CountCategories(Without(q with { CommunityVote = null }), r => r.CommunityVote),
            Without(q with { LegacySlots = null }).Where(r => r.Chart.Slot != null)
                .GroupBy(r => r.Chart.Slot!.Value).ToDictionary(g => g.Key, g => g.Count()),
            Without(q with { DebutMixes = null }).GroupBy(r => r.DebutMix)
                .ToDictionary(g => g.Key, g => g.Count()),
            myRecords == null
                ? new Dictionary<ChartScoreStateFilter, int>()
                : Without(q with { ScoreStates = null })
                    .GroupBy(r => StateOf(r, myRecords))
                    .ToDictionary(g => g.Key, g => g.Count()),
            Without(q with { CoOpPlayerCounts = null }).Where(r => r.Chart.Type == ChartType.CoOp)
                .GroupBy(r => r.Chart.PlayerCount).ToDictionary(g => g.Key, g => g.Count()));
    }

    /// <summary>No record at all is unplayed; otherwise the record's pass flag decides.</summary>
    private static ChartScoreStateFilter StateOf(ChartSearchResult row,
        IReadOnlyDictionary<Guid, MyRecord> records)
    {
        if (!records.TryGetValue(row.Chart.Id, out var record)) return ChartScoreStateFilter.Unplayed;
        return record.Passed ? ChartScoreStateFilter.Passed : ChartScoreStateFilter.Failed;
    }

    private static ChartSearchResult Project(Chart chart,
        IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>> metricsByChart, CommunityBundle bundle)
    {
        var id = chart.Id;
        var badges = Array.Empty<ChartBadge>();
        decimal? nps = null;
        if (metricsByChart.TryGetValue(id, out var metrics))
        {
            badges = metrics
                .Where(m => m.MetricName.StartsWith(PiuCenterMetrics.Top3Prefix, StringComparison.Ordinal))
                .Select(m => m.MetricName[PiuCenterMetrics.Top3Prefix.Length..])
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => new ChartBadge(k, BadgeLabels.DisplayName(k), BadgeLabels.CategoryFor(k)))
                .ToArray();
            nps = metrics.FirstOrDefault(m => m.MetricName == PiuCenterMetrics.Nps)?.Value;
        }

        var aggregate = bundle.Aggregates.TryGetValue(id, out var agg) ? agg : null;

        return new ChartSearchResult(
            chart,
            chart.OriginalMix,
            badges,
            nps,
            bundle.PassDifficulty.TryGetValue(id, out var pass) ? pass : null,
            bundle.ScoreDifficulty.TryGetValue(id, out var score) ? score : null,
            bundle.CommunityVote.TryGetValue(id, out var vote) ? vote : null,
            bundle.ScoringLevels.TryGetValue(id, out var level) ? level : null,
            bundle.VoteRatings.TryGetValue(id, out var rating) ? rating : null,
            aggregate?.Count ?? 0,
            aggregate?.PgCount ?? 0,
            null);
    }

    private static IReadOnlyDictionary<TierListCategory, int> CountCategories(
        IEnumerable<ChartSearchResult> rows, Func<ChartSearchResult, TierListCategory?> category)
    {
        return rows.Where(r => category(r) != null)
            .GroupBy(r => category(r)!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private sealed record CommunityBundle(
        IReadOnlyDictionary<Guid, TierListCategory> PassDifficulty,
        IReadOnlyDictionary<Guid, TierListCategory> ScoreDifficulty,
        IReadOnlyDictionary<Guid, TierListCategory> CommunityVote,
        IReadOnlyDictionary<Guid, double> ScoringLevels,
        IReadOnlyDictionary<Guid, double> VoteRatings,
        IReadOnlyDictionary<Guid, ChartScoreAggregate> Aggregates);

    private async Task<CommunityBundle> GetCommunityBundle(MixEnum mix, CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync($"ChartSearch__Community__{mix}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = UntilNextRecompute();

            var aggregates = (await _scores.GetChartScoreAggregates(mix, cancellationToken))
                .ToDictionary(a => a.ChartId);

            if (mix.UsesLegacyScoring())
            {
                // XX and older: the Community Vote list is the only difficulty signal.
                var voteTiers = (await _tierLists.GetAllEntries(mix, "Difficulty", cancellationToken))
                    .ToDictionary(e => e.ChartId, e => e.Category);
                var voteRatings = (await _ratings.GetAllChartRatedDifficulties(mix, cancellationToken))
                    .ToDictionary(r => r.ChartId, r => r.Difficulty);
                return new CommunityBundle(
                    new Dictionary<Guid, TierListCategory>(),
                    new Dictionary<Guid, TierListCategory>(),
                    voteTiers,
                    new Dictionary<Guid, double>(),
                    voteRatings,
                    aggregates);
            }

            var passTiers = (await _tierLists.GetAllEntries(mix, "Pass Count", cancellationToken))
                .ToDictionary(e => e.ChartId, e => e.Category);
            var scoreTiers = (await _tierLists.GetAllEntries(mix, "Scores", cancellationToken))
                .ToDictionary(e => e.ChartId, e => e.Category);
            var scoringLevels = (await _scoringLevels.GetScoringLevels(mix, cancellationToken))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            return new CommunityBundle(passTiers, scoreTiers,
                new Dictionary<Guid, TierListCategory>(),
                scoringLevels,
                new Dictionary<Guid, double>(),
                aggregates);
        }))!;
    }

    /// <summary>The analytics chain ends by 12:xx UTC; search bundles refresh just after 13:00.</summary>
    private TimeSpan UntilNextRecompute()
    {
        var now = _clock.Now;
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddHours(13);
        return (now < today ? today : today.AddDays(1)) - now;
    }

    private static bool MatchesContent(ChartSearchResult row, SearchChartsQuery q)
    {
        var chart = row.Chart;
        var song = chart.Song;

        if (q.RestrictToChartIds is { Count: > 0 } && !q.RestrictToChartIds.Contains(chart.Id)) return false;
        if (!string.IsNullOrWhiteSpace(q.SongNameContains) &&
            !song.Name.ToString().Contains(q.SongNameContains.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (q.LevelMin != null || q.LevelMax != null)
        {
            var min = q.LevelMin ?? int.MinValue;
            var max = q.LevelMax ?? int.MaxValue;
            if ((int)chart.Level < min || (int)chart.Level > max) return false;
        }

        if (q.Types is { Count: > 0 } && !q.Types.Contains(chart.Type)) return false;
        if (q.CoOpPlayerCounts is { Count: > 0 } &&
            (chart.Type != ChartType.CoOp || !q.CoOpPlayerCounts.Contains(chart.PlayerCount)))
            return false;
        if (q.SongTypes is { Count: > 0 } && !q.SongTypes.Contains(song.Type)) return false;
        if (q.Artists is { Count: > 0 } &&
            !q.Artists.Any(a => string.Equals(a, song.Artist.ToString(), StringComparison.OrdinalIgnoreCase)))
            return false;
        if (q.StepArtists is { Count: > 0 } &&
            (chart.StepArtist == null || !q.StepArtists.Any(a =>
                string.Equals(a, chart.StepArtist.Value.ToString(), StringComparison.OrdinalIgnoreCase))))
            return false;

        if (q.BpmMin != null || q.BpmMax != null)
        {
            if (song.Bpm == null) return false;
            if (q.BpmMin != null && song.Bpm.Value.Max < q.BpmMin.Value) return false;
            if (q.BpmMax != null && song.Bpm.Value.Min > q.BpmMax.Value) return false;
        }

        if (q.DurationMin != null && song.Duration < q.DurationMin.Value) return false;
        if (q.DurationMax != null && song.Duration > q.DurationMax.Value) return false;

        if (q.NoteCountMin != null && (chart.NoteCount == null || chart.NoteCount < q.NoteCountMin)) return false;
        if (q.NoteCountMax != null && (chart.NoteCount == null || chart.NoteCount > q.NoteCountMax)) return false;
        if (q.NpsMin != null && (row.Nps == null || row.Nps < q.NpsMin)) return false;
        if (q.NpsMax != null && (row.Nps == null || row.Nps > q.NpsMax)) return false;

        if (q.Badges is { Count: > 0 } &&
            !row.Badges.Any(b => q.Badges.Contains(b.Key, StringComparer.OrdinalIgnoreCase)))
            return false;

        if (q.DebutMixes is { Count: > 0 } && !q.DebutMixes.Contains(row.DebutMix)) return false;

        if (q.LegacySlots is { Count: > 0 } &&
            (chart.Slot == null || !q.LegacySlots.Contains(chart.Slot.Value)))
            return false;

        return true;
    }

    private static bool MatchesCommunity(ChartSearchResult row, SearchChartsQuery q)
    {
        if (q.PassDifficulty is { Count: > 0 } &&
            (row.PassDifficulty == null || !q.PassDifficulty.Contains(row.PassDifficulty.Value)))
            return false;
        if (q.ScoreDifficulty is { Count: > 0 } &&
            (row.ScoreDifficulty == null || !q.ScoreDifficulty.Contains(row.ScoreDifficulty.Value)))
            return false;
        if (q.CommunityVote is { Count: > 0 } &&
            (row.CommunityVote == null || !q.CommunityVote.Contains(row.CommunityVote.Value)))
            return false;

        if (q.ScoringLevelMin != null && (row.ScoringLevel == null || row.ScoringLevel < q.ScoringLevelMin))
            return false;
        if (q.ScoringLevelMax != null && (row.ScoringLevel == null || row.ScoringLevel > q.ScoringLevelMax))
            return false;

        return true;
    }

    private sealed record MyRecord(bool Passed, bool IsBroken, int? PhoenixScore, PhoenixLetterGrade? PhoenixGrade,
        PhoenixPlate? PhoenixPlate, XXLetterGrade? LegacyGrade, int? LegacyScore, DateTimeOffset? RecordedOn,
        JudgementCounts? Judgements);

    /// <summary>The visitor's best per chart in this mix, family-shaped by its scoring model.</summary>
    private async Task<IReadOnlyDictionary<Guid, MyRecord>> LoadMyRecords(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        var records = new Dictionary<Guid, MyRecord>();
        if (mix.UsesLegacyScoring())
            foreach (var attempt in await _scores.GetBestXXAttempts(mix, userId, cancellationToken))
            {
                var best = attempt.BestAttempt;
                if (best == null) continue;
                // A legacy result screen has no judgement breakdown to observe.
                records[attempt.Chart.Id] = new MyRecord(!best.IsBroken, best.IsBroken, null, null, null,
                    best.LetterGrade, best.Score, best.RecordedOn, null);
            }
        else
            foreach (var record in await _scores.GetBestScores(mix, userId, cancellationToken))
                // Grades resolve against the mix — Phoenix 2 shifted the A/A+/AA/AA+ floors,
                // so a shared score means different letters per mix.
                records[record.ChartId] = new MyRecord(!record.IsBroken, record.IsBroken,
                    record.Score == null ? null : (int)record.Score.Value, record.Score?.LetterGradeFor(mix),
                    record.Plate, null, null, record.RecordedDate, record.Judgements);

        return records;
    }

    private static ChartSearchMyState? BuildMyState(ChartSearchResult row,
        IReadOnlyDictionary<Guid, MyRecord> records)
    {
        if (!records.TryGetValue(row.Chart.Id, out var record)) return null;

        return new ChartSearchMyState(record.PhoenixScore, record.PhoenixGrade, record.PhoenixPlate,
            record.LegacyGrade, record.LegacyScore, record.IsBroken, record.Passed, record.RecordedOn,
            record.Judgements);
    }

    private static bool MatchesUser(ChartSearchResult row, SearchChartsQuery q,
        IReadOnlyDictionary<Guid, MyRecord> records)
    {
        var hasRecord = records.TryGetValue(row.Chart.Id, out var record);

        if (q.ScoreStates is { Count: > 0 })
        {
            var matches = q.ScoreStates.Any(state => state switch
            {
                ChartScoreStateFilter.Unplayed => !hasRecord,
                ChartScoreStateFilter.Played => hasRecord,
                ChartScoreStateFilter.Passed => record is { Passed: true },
                ChartScoreStateFilter.Failed => record is { Passed: false },
                _ => true
            });
            if (!matches) return false;
        }

        if (q.RecordedFrom != null && (row.My?.RecordedOn == null || row.My.RecordedOn < q.RecordedFrom))
            return false;
        if (q.RecordedTo != null && (row.My?.RecordedOn == null || row.My.RecordedOn > q.RecordedTo))
            return false;

        // Family facets judge a row by its own family only — Phoenix filters can never
        // exclude a legacy row on Phoenix terms, and vice versa. When only the other
        // family's filters are set, the row has no way to satisfy the expressed intent.
        var phoenixSet = q.PhoenixGradeMin != null || q.PhoenixPlates is { Count: > 0 } ||
                         q.PhoenixScoreMin != null || q.PhoenixScoreMax != null;
        var legacySet = q.LegacyGradeMin != null;
        if (phoenixSet || legacySet)
        {
            if (row.Chart.Mix.UsesLegacyScoring())
            {
                if (!legacySet) return false;
                if (row.My?.LegacyGrade == null || row.My.LegacyGrade < q.LegacyGradeMin) return false;
            }
            else
            {
                if (!phoenixSet) return false;
                if (q.PhoenixGradeMin != null &&
                    (row.My?.PhoenixGrade == null || row.My.PhoenixGrade < q.PhoenixGradeMin)) return false;
                if (q.PhoenixPlates is { Count: > 0 } &&
                    (row.My?.PhoenixPlate == null ||
                     !q.PhoenixPlates.Contains(row.My.PhoenixPlate.Value))) return false;
                if (q.PhoenixScoreMin != null &&
                    (row.My?.PhoenixScore == null || row.My.PhoenixScore < q.PhoenixScoreMin)) return false;
                if (q.PhoenixScoreMax != null &&
                    (row.My?.PhoenixScore == null || row.My.PhoenixScore > q.PhoenixScoreMax)) return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     The family-safe difficulty decimal: scoring level for Phoenix-family results,
    ///     Community Vote average for XX-and-older. Falls back to the printed level.
    /// </summary>
    private static double DifficultySignal(ChartSearchResult row)
    {
        var signal = row.Chart.Mix.UsesLegacyScoring() ? row.CommunityVoteRating : row.ScoringLevel;
        return signal ?? (int)row.Chart.Level + .5;
    }

    private static IEnumerable<ChartSearchResult> Sort(IEnumerable<ChartSearchResult> rows, SearchChartsQuery q)
    {
        var desc = q.SortDescending;

        IOrderedEnumerable<ChartSearchResult> By<TKey>(Func<ChartSearchResult, TKey> key)
        {
            return desc ? rows.OrderByDescending(key) : rows.OrderBy(key);
        }

        // Null facet values sort to the tail regardless of direction.
        IOrderedEnumerable<ChartSearchResult> NullsLast(Func<ChartSearchResult, double?> key)
        {
            var present = rows.OrderBy(r => key(r) == null ? 1 : 0);
            return desc ? present.ThenByDescending(r => key(r) ?? 0) : present.ThenBy(r => key(r) ?? 0);
        }

        var ordered = q.Sort switch
        {
            // Within a level the difficulty signal keeps the gradient smooth, following
            // the level direction (Level ↓ reads hardest-of-the-folder first).
            ChartSearchSort.Level => desc
                ? rows.OrderByDescending(r => (int)r.Chart.Level).ThenByDescending(DifficultySignal)
                : rows.OrderBy(r => (int)r.Chart.Level).ThenBy(DifficultySignal),
            ChartSearchSort.ScoringLevel => NullsLast(DifficultySignalOrNull),
            ChartSearchSort.Popularity => By(r => r.ScoreCount),
            // Pass Difficulty is the community lens, not the raw rate: the ramp already encodes
            // "hard for its level", where a rate needs a sample floor to mean anything.
            ChartSearchSort.PassDifficulty => NullsLast(r => r.PassDifficulty == null ? null : (double)r.PassDifficulty.Value),
            ChartSearchSort.DebutEra => By(r => r.DebutMix.DisplayOrder()),
            ChartSearchSort.Name => By(r => r.Chart.Song.Name.ToString()),
            ChartSearchSort.Bpm => NullsLast(r => r.Chart.Song.Bpm == null ? null : (double)r.Chart.Song.Bpm.Value.Max),
            ChartSearchSort.Nps => NullsLast(r => r.Nps == null ? null : (double)r.Nps.Value),
            ChartSearchSort.Duration => By(r => r.Chart.Song.Duration),
            // Families never compare: unscored rows tail, then Phoenix scores order the
            // Phoenix-family rows and legacy scores the rest — grouping falls out of the keys.
            ChartSearchSort.MyGrade => desc
                ? rows.OrderBy(r => r.My?.PhoenixScore == null && r.My?.LegacyScore == null ? 1 : 0)
                    .ThenByDescending(r => r.My?.PhoenixScore ?? int.MinValue)
                    .ThenByDescending(r => r.My?.LegacyScore ?? int.MinValue)
                : rows.OrderBy(r => r.My?.PhoenixScore == null && r.My?.LegacyScore == null ? 1 : 0)
                    .ThenBy(r => r.My?.PhoenixScore ?? int.MaxValue)
                    .ThenBy(r => r.My?.LegacyScore ?? int.MaxValue),
            ChartSearchSort.MyRecent => NullsLast(r => (double?)r.My?.RecordedOn?.UtcTicks),
            _ => By(r => (int)r.Chart.Level)
        };

        return ordered
            .ThenBy(r => r.Chart.Song.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Chart.Id);
    }

    private static double? DifficultySignalOrNull(ChartSearchResult row)
    {
        return row.Chart.Mix.UsesLegacyScoring() ? row.CommunityVoteRating : row.ScoringLevel;
    }
}
