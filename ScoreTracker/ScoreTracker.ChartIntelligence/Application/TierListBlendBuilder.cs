using MediatR;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The blend's source computation, extracted from BlendedTierListHandler so the
///     Personalized Breakdown query can expose the same numbers the blend actually
///     uses (breakdown-page workshop) — one implementation, two consumers, no drift.
///     Owns the lens weight tables and the score projection.
/// </summary>
internal sealed class TierListBlendBuilder
{
    /// <summary>
    ///     What the shared, community view of a lens is made of — stored lists only. This is
    ///     also what a signed-out visitor sees, and what the personalized view is diffed against.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> CommunityModifiers =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pass"] = new Dictionary<string, double> { ["Pass Count"] = 2 },
            ["Score"] = new Dictionary<string, double> { ["Official Scores"] = 1, ["Scores"] = 2 },
            ["Popularity"] = new Dictionary<string, double> { ["Popularity"] = 1 },
            ["Chabala"] = new Dictionary<string, double> { ["Chabala"] = 1 },
            ["PG"] = new Dictionary<string, double> { ["PG"] = 1 },
            ["PUMBILITY"] = new Dictionary<string, double> { [PumbilitySource] = 1 }
        };

    /// <summary>
    ///     What the personalized view of a lens is made of. Only Score has one; every other lens
    ///     is community-only and personalizing it would mean nothing.
    ///     <para>
    ///         Score is the projection and nothing else (owner, 2026-08-11). Blending the stored
    ///         score lists back in would count the same evidence twice: the projection is built
    ///         from peers' actual scores, so those lists are an echo of its own input, bucketed —
    ///         not a second opinion. It also means the standard-deviation banding happens once,
    ///         inside the projection, rather than being averaged with other bandings and re-cut.
    ///     </para>
    ///     <para>
    ///         Pass no longer personalizes at all (owner, 2026-08-13). Its personal half was the
    ///         skill estimate and the similar-players aggregation, and neither carried a pass
    ///         signal — both were built from scores — so roughly 60% of a "personalized pass"
    ///         answer was score inference wearing a pass label. What replaces it is the PUMBILITY
    ///         lens; see docs/design/pumbility-tier-list.md.
    ///     </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> PersonalizedModifiers =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Score"] = new Dictionary<string, double> { ["Projection"] = 1 },
            // Same computation as the community view, over a different set of players — so the
            // recipe is identical and only the cohort the census is read for changes.
            ["PUMBILITY"] = new Dictionary<string, double> { [PumbilitySource] = 1 }
        };

    /// <summary>The PUMBILITY source's name, which is its own single-source recipe on both views.</summary>
    private const string PumbilitySource = "PUMBILITY";

    /// <summary>A PUMBILITY pool is fifty charts; anything short of that is not one yet.</summary>
    private const int PumbilityPoolSize = 50;

    private static readonly string[] StoredSources =
        { "Official Scores", "Scores", "Popularity", "Pass Count", "PG", "Chabala" };

    private readonly IChartRepository _charts;
    private readonly IMediator _mediator;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreProjector _projector;
    private readonly IScoreReader _scores;
    private readonly ITierListRepository _tierLists;
    private readonly ITitleRepository _titles;

    public TierListBlendBuilder(IMediator mediator, IChartRepository charts, IScoreProjector projector,
        ITierListRepository tierLists, ITitleRepository titles, IPlayerStatsReader playerStats,
        IScoreReader scores)
    {
        _mediator = mediator;
        _charts = charts;
        _projector = projector;
        _tierLists = tierLists;
        _titles = titles;
        _playerStats = playerStats;
        _scores = scores;
    }

    /// <summary>How much of a recipe is stored community lists — 0 when none of it is.</summary>
    public static double CommunityWeightIn(IReadOnlyDictionary<string, double> modifiers)
    {
        return modifiers.Where(kv => StoredSources.Contains(kv.Key)).Sum(kv => kv.Value);
    }

    public static bool IsKnownLens(string lens)
    {
        return CommunityModifiers.ContainsKey(lens);
    }

    /// <summary>
    ///     The weights in play for a lens as one view or the other. A lens with no personalized
    ///     recipe falls back to its community one, so asking for a personalized Popularity list
    ///     gets the community answer rather than an empty page.
    /// </summary>
    public static IReadOnlyDictionary<string, double> ModifiersFor(string lens, bool personalized)
    {
        return personalized && PersonalizedModifiers.TryGetValue(lens, out var mine)
            ? mine
            : CommunityModifiers[lens];
    }

    public async Task<BlendComputation> Compute(ChartType chartType, DifficultyLevel level, string lens,
        Guid? userId, MixEnum mix, CancellationToken cancellationToken)
    {
        var modifiers = ModifiersFor(lens, userId != null);
        var folderCharts =
            (await _charts.GetCharts(mix, level, chartType, cancellationToken: cancellationToken)).ToArray();

        var sources = new Dictionary<string, IReadOnlyDictionary<Guid, SongTierListEntry>>();
        var provisional = false;
        foreach (var sourceName in StoredSources)
        {
            if (!modifiers.TryGetValue(sourceName, out var weight) || weight <= 0) continue;
            var result = await _mediator.Send(new GetTierListWithFallbackQuery(sourceName, mix),
                cancellationToken);
            provisional |= result.IsProvisionalFallback;
            sources[sourceName] = result.Entries
                .GroupBy(e => e.ChartId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        PumbilityComputation? pumbility = null;
        if (modifiers.ContainsKey(PumbilitySource))
        {
            pumbility = await ComputePumbility(chartType, level, mix, userId, cancellationToken);
            sources[PumbilitySource] = pumbility.Entries;
        }

        ProjectionComputation? projection = null;
        if (userId != null && modifiers.TryGetValue("Projection", out var projectionWeight) && projectionWeight > 0)
        {
            projection = await ComputeProjection(chartType, mix, userId.Value, folderCharts,
                cancellationToken);
            sources["Projection"] = projection.Entries;
        }

        return new BlendComputation(folderCharts, sources, modifiers, provisional, projection, pumbility);
    }

    /// <summary>
    ///     The per-chart weighted combine — identical math for the blend's final list
    ///     and the breakdown's community/personalized columns. Sources without an
    ///     entry (or with Unrecorded) simply don't vote; no votes at all = Unrecorded.
    /// </summary>
    public static SongTierListEntry Combine(string listName, Guid chartId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<Guid, SongTierListEntry>> sources,
        IReadOnlyDictionary<string, double> modifiers)
    {
        var weightTotal = 0.0;
        var weightedScore = 0.0;
        foreach (var (sourceName, sourceEntries) in sources)
        {
            if (!modifiers.TryGetValue(sourceName, out var weight) || weight <= 0) continue;
            if (!sourceEntries.TryGetValue(chartId, out var entry) ||
                entry.Category == TierListCategory.Unrecorded) continue;
            weightTotal += weight;
            weightedScore += weight * ((int)entry.Category - 3);
        }

        if (weightTotal < .0001) return new SongTierListEntry(listName, chartId, TierListCategory.Unrecorded, 999999);

        var final = weightedScore / weightTotal;
        return new SongTierListEntry(listName, chartId,
            final < -2.5 ? TierListCategory.Overrated :
            final < -1.5 ? TierListCategory.VeryEasy :
            final < -.5 ? TierListCategory.Easy :
            final <= .5 ? TierListCategory.Medium :
            final <= 1.5 ? TierListCategory.Hard :
            final <= 2.5 ? TierListCategory.VeryHard :
            TierListCategory.Underrated, (int)(final * 100.0));
    }

    /// <summary>
    ///     Reads the materialized PUMBILITY tier list for this folder, for everyone or for the
    ///     viewer's own cohort. Nothing is computed here — the nightly job owns the counting,
    ///     and a cohort with no rows for this folder simply votes on nothing.
    /// </summary>
    private async Task<PumbilityComputation> ComputePumbility(ChartType chartType, DifficultyLevel level,
        MixEnum mix, Guid? userId, CancellationToken cancellationToken)
    {
        var cohortKey = userId == null
            ? PumbilityCohortKeys.Community
            : await ResolveViewerCohort(chartType, mix, userId.Value, cancellationToken);
        if (cohortKey == null)
            return new PumbilityComputation(new Dictionary<Guid, SongTierListEntry>(),
                new Dictionary<Guid, int>(), 0);

        var folder = await _tierLists.GetPumbilityTierList(mix, chartType, level, cohortKey, cancellationToken);
        return new PumbilityComputation(
            folder.Entries.ToDictionary(e => e.ChartId,
                e => new SongTierListEntry(PumbilitySource, e.ChartId, e.Category, e.Order)),
            folder.Entries.ToDictionary(e => e.ChartId, e => e.Appearances),
            folder.CohortSize);
    }

    /// <summary>
    ///     Which cohort the reader belongs to, resolved exactly as the nightly job resolves it —
    ///     Phoenix 1 by the level of their highest difficulty title, Phoenix 2 by the PUMBILITY
    ///     rung their pool clears. The two must agree or a player reads a list nobody built for
    ///     them.
    /// </summary>
    public async Task<string?> ResolveViewerCohort(ChartType chartType, MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        if (mix != MixEnum.Phoenix2)
        {
            var titleLevel = await _titles.GetCurrentTitleLevel(mix, userId, cancellationToken);
            return PumbilityCohortKeys.ForDifficultyTitleLevel((int)titleLevel);
        }

        // Same shape of gate the census applies. A reader who has not yet imported a pool's
        // worth of this mix resolves to a rung well below where they play — their total is low
        // because they have played little of it, not because they are weak — and is then handed
        // the folder band of the players genuinely at that rung. Counting scores rather than the
        // pool itself is deliberately coarse: it is the mix-has-no-volume-yet case this exists
        // for, and it costs one read behind a six-hour cache.
        var scored = (await _scores.GetBestScores(mix, userId, cancellationToken))
            .Count(s => s is { Score: not null, IsBroken: false });
        if (scored < PumbilityPoolSize) return null;

        var stats = await _playerStats.GetStats(mix, userId, cancellationToken);
        return PumbilityCohortKeys.ForPhoenix2Pool(chartType,
            chartType == ChartType.Single ? stats.SinglesRating : stats.DoublesRating);
    }

    /// <summary>
    ///     Competitive-level half-width for the projection's peer gate. Narrower than the
    ///     PUMBILITY page's ±1.0, and deliberately: that page quotes the projected number, where
    ///     the wider window is measured more accurate, while this one only ranks the folder's
    ///     charts against each other. ±0.5 is what the rest of the site means by a competitive
    ///     peer — the session breakdown, communities, player highlights and this page's own
    ///     Vs. Peers column all use it — and a level and a half of spread is two different players.
    /// </summary>
    public const double ProjectionCompetitiveWindow = 0.5;

    /// <summary>
    ///     How many of a folder's charts the projection must reach before it votes. The tier
    ///     bands are cut from the spread of the values handed in, so a lone projection sits at
    ///     its own mean with a standard deviation of zero and comes out stamped the easiest chart
    ///     in the folder — at full weight, off one peer's single score. Two is barely better: the
    ///     pair stretches to opposite ends of the ramp whatever the gap between them.
    ///     ⚠ Provisional, and one of the numbers ScoreProjectionCostProbeTests exists to settle.
    /// </summary>
    public const int MinProjectedCharts = 3;

    // The personalized Score source: what players at this level actually score on these charts,
    // bucketed by standard deviation like every other tier list. A chart no peer has played is
    // simply absent from the result — Combine skips a source with no entry, so the community
    // lists carry that chart on their own rather than it dropping out of the folder.
    private async Task<ProjectionComputation> ComputeProjection(ChartType chartType, MixEnum mix, Guid userId,
        IReadOnlyCollection<Chart> folderCharts, CancellationToken cancellationToken)
    {
        var projection = await _projector.Project(new ScoreProjectionRequest(mix, chartType, userId,
                folderCharts.Select(c => new ProjectionTarget(c.Id, (int)c.Level)).ToArray(),
                ProjectionCompetitiveWindow),
            cancellationToken);
        // A projection of exactly zero is dropped rather than passed on. PhoenixScore's floor IS
        // zero, so it is a value peers can hold, and TierListProcessor reserves zero for "no
        // opinion" — so a chart landing there would go Not Rated on the list while the breakdown
        // drew a confident 0 for it. The two surfaces have to agree about which charts the
        // projection answered for, and this is the only value where they could not.
        var projected = projection.Scores.Count == 0
            ? projection.Scores
            : projection.Scores.Where(kv => (int)kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        // The raw scores travel with the buckets: the breakdown page is built on the numbers
        // themselves — where each chart sits in the folder's spread, and how that compares to
        // what the player actually scored — and re-deriving them there would mean a second
        // projection of the same folder.
        if (projected.Count < MinProjectedCharts)
            return new ProjectionComputation(new Dictionary<Guid, SongTierListEntry>(), projected,
                projected.Count, folderCharts.Count, projection.PeerCount, projection.CompetitiveLevel,
                projection.MeanFreshness);

        var estimates = projected.ToDictionary(kv => kv.Key, kv => (double)(int)kv.Value);
        return new ProjectionComputation(
            TierListProcessor.ProcessIntoTierList("Projection", estimates)
                .ToDictionary(e => e.ChartId, e => e),
            projected,
            projected.Count,
            folderCharts.Count,
            projection.PeerCount,
            projection.CompetitiveLevel,
            projection.MeanFreshness);
    }
}

/// <summary>Everything one blend run computed, for consumers that need the internals.</summary>
internal sealed record BlendComputation(
    IReadOnlyList<Chart> FolderCharts,
    IReadOnlyDictionary<string, IReadOnlyDictionary<Guid, SongTierListEntry>> Sources,
    IReadOnlyDictionary<string, double> Modifiers,
    bool IsProvisionalFallback,
    ProjectionComputation? Projection,
    PumbilityComputation? Pumbility);

/// <summary>
///     The PUMBILITY source's output: the banded entries, how many of the cohort's pools hold
///     each chart, and how many players the cohort is. An empty read is the honest answer for a
///     folder this cohort's pools cannot reach.
/// </summary>
internal sealed record PumbilityComputation(
    IReadOnlyDictionary<Guid, SongTierListEntry> Entries,
    IReadOnlyDictionary<Guid, int> Appearances,
    int CohortSize);

/// <summary>
///     The projection source's output plus what the page needs in order to say something true
///     when it is quiet: how many of the folder's charts peers at this level have played at all,
///     and the cohort the numbers came from.
/// </summary>
internal sealed record ProjectionComputation(
    IReadOnlyDictionary<Guid, SongTierListEntry> Entries,
    IReadOnlyDictionary<Guid, PhoenixScore> Scores,
    int ProjectedChartCount,
    int FolderChartCount,
    int PeerCount,
    double CompetitiveLevel,
    double MeanFreshness);
