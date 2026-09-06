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
            // recipe is identical and only the peer group the census is read for changes.
            ["PUMBILITY"] = new Dictionary<string, double> { [PumbilitySource] = 1 }
        };

    /// <summary>The PUMBILITY source's name, which is its own single-source recipe on both views.</summary>
    private const string PumbilitySource = "PUMBILITY";

    private static readonly string[] StoredSources =
        { "Official Scores", "Scores", "Popularity", "Pass Count", "PG", "Chabala" };

    private readonly IChartRepository _charts;
    private readonly IMediator _mediator;
    private readonly IScoreProjector _projector;
    private readonly IScoreReader _scores;
    private readonly ITierListRepository _tierLists;
    private readonly ITitleRepository _titles;

    public TierListBlendBuilder(IMediator mediator, IChartRepository charts, IScoreProjector projector,
        ITierListRepository tierLists, ITitleRepository titles, IScoreReader scores)
    {
        _mediator = mediator;
        _charts = charts;
        _projector = projector;
        _tierLists = tierLists;
        _titles = titles;
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
            pumbility = await ComputePumbility(chartType, level, mix, userId, folderCharts, cancellationToken);
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
    ///     The PUMBILITY source for this folder: the community's stored list for everyone, the
    ///     projector's peers' pools for a signed-in Phoenix 2 viewer (D55), and the stored
    ///     title-level list for a signed-in Phoenix 1 viewer. A peer group whose pools reach
    ///     nothing in this folder simply votes on nothing.
    /// </summary>
    private async Task<PumbilityComputation> ComputePumbility(ChartType chartType, DifficultyLevel level,
        MixEnum mix, Guid? userId, IReadOnlyCollection<Chart> folderCharts, CancellationToken cancellationToken)
    {
        if (userId != null && mix == MixEnum.Phoenix2)
            return await ProjectedPumbility(chartType, mix, userId.Value, folderCharts, cancellationToken);

        var (peerKey, ownPool) = userId == null
            ? (PumbilityPeers.Community, null)
            : await ResolveViewerPeersAndPool(chartType, mix, userId.Value, cancellationToken);
        if (peerKey == null)
            return new PumbilityComputation(new Dictionary<Guid, SongTierListEntry>(),
                new Dictionary<Guid, int>(), 0);

        var folder = await _tierLists.GetPumbilityTierList(mix, chartType, level, peerKey, cancellationToken);
        if (ownPool == null || folder.Entries.Count == 0)
            return new PumbilityComputation(
                folder.Entries.ToDictionary(e => e.ChartId,
                    e => new SongTierListEntry(PumbilitySource, e.ChartId, e.Category, e.Order)),
                folder.Entries.ToDictionary(e => e.ChartId, e => e.Appearances),
                folder.PeerCount);

        // A player is never one of their own peers (owner, 2026-08-17). The stored Phoenix 1 list is
        // one per peer group and counts every member's pool — the viewer's among them when they
        // hold one — so the viewer's own is taken back out here: one from the peer count, one from
        // every chart their pool holds, and the bands redrawn over what is left with the processor
        // the nightly job used. Nightly is the caveat: a pool that filled since the last build was
        // never counted in, and for that day the subtraction runs one deep. If nothing is left the
        // peer group votes on nothing here, exactly as the writer would have left it.
        var counts = folder.Entries.ToDictionary(e => e.ChartId,
            e => Math.Max(0, e.Appearances - (ownPool.Contains(e.ChartId) ? 1 : 0)));
        if (counts.Values.Sum() == 0)
            return new PumbilityComputation(new Dictionary<Guid, SongTierListEntry>(),
                new Dictionary<Guid, int>(), Math.Max(0, folder.PeerCount - 1));

        return new PumbilityComputation(
            TierListProcessor.ProcessIntoLogScaledTierList(PumbilitySource, counts)
                .ToDictionary(e => e.ChartId),
            counts,
            Math.Max(0, folder.PeerCount - 1));
    }

    /// <summary>
    ///     Phoenix 2, signed in: the viewer's PUMBILITY peers as the projector draws them (D53) —
    ///     the players whose pool of the type sits within the window around the viewer's own, each
    ///     holding a full pool of it, the viewer out — and how many of their pools hold each of the
    ///     folder's charts, banded with the writer's own processor. One definition across the site
    ///     (D55): this is the same <see cref="PeerPoolSummary" /> the PUMBILITY page's Play list
    ///     counts, so the lens and that page cannot disagree about who the peers are or what they
    ///     hold. A viewer short of a full pool of the type has no peers for it (D28) and votes on
    ///     nothing — the projector says so on the group, and the empty answer is cached briefly.
    ///     No thin-band fallback and no short-pool finish are asked for: the list ranks charts
    ///     against each other, and the community list is a better answer than a folder ranked on
    ///     one pool.
    /// </summary>
    private async Task<PumbilityComputation> ProjectedPumbility(ChartType chartType, MixEnum mix, Guid userId,
        IReadOnlyCollection<Chart> folderCharts, CancellationToken cancellationToken)
    {
        var catalog = (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var projection = await _projector.Project(new ScoreProjectionRequest(mix, chartType, userId,
                folderCharts.Select(c => new ProjectionTarget(c.Id, (int)c.Level)).ToArray(),
                ProjectionCompetitiveWindow, catalog),
            cancellationToken);
        if (projection.PeerPools is not { } pools)
            return new PumbilityComputation(new Dictionary<Guid, SongTierListEntry>(),
                new Dictionary<Guid, int>(), projection.Group?.Size ?? 0);

        var counts = folderCharts.ToDictionary(c => c.Id,
            c => pools.Charts.TryGetValue(c.Id, out var chart) ? chart.Holders : 0);
        if (counts.Values.Sum() == 0)
            return new PumbilityComputation(new Dictionary<Guid, SongTierListEntry>(), counts, pools.Peers.Count);

        return new PumbilityComputation(
            TierListProcessor.ProcessIntoLogScaledTierList(PumbilitySource, counts).ToDictionary(e => e.ChartId),
            counts,
            pools.Peers.Count);
    }

    /// <summary>
    ///     Phoenix 2, signed in: the folders the viewer's PUMBILITY peers' pools reach — every
    ///     level at which at least one peer holds a chart of the type in their top fifty, from the
    ///     pool floor up. The same projector read as <see cref="ProjectedPumbility" />, over every
    ///     chart of the type, so the picker offers exactly the folders the lens can answer for.
    ///     Empty for a viewer without a full pool of the type.
    /// </summary>
    public async Task<IReadOnlyList<int>> ProjectedPumbilityFolders(ChartType chartType, MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        var catalog = (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var ofType = catalog.Values
            .Where(c => c.Type == chartType && (int)c.Level >= (int)PeerGroup.PumbilityPoolFloor)
            .Select(c => new ProjectionTarget(c.Id, (int)c.Level))
            .ToArray();
        if (ofType.Length == 0) return Array.Empty<int>();

        var projection = await _projector.Project(
            new ScoreProjectionRequest(mix, chartType, userId, ofType, ProjectionCompetitiveWindow, catalog),
            cancellationToken);
        if (projection.PeerPools is not { } pools) return Array.Empty<int>();

        return pools.Charts
            .Where(kv => kv.Value.Holders > 0 && catalog.ContainsKey(kv.Key))
            .Select(kv => (int)catalog[kv.Key].Level)
            .Where(level => level >= (int)PeerGroup.PumbilityPoolFloor)
            .Distinct()
            .OrderBy(level => level)
            .ToArray();
    }

    /// <summary>
    ///     Phoenix 1: which stored peer group the reader belongs to, resolved exactly as the nightly
    ///     job resolves it — the level of their highest difficulty title. The two must agree or a
    ///     player reads a list nobody built for them. Phoenix 2 stores no personalized list
    ///     (D55): ask <see cref="ProjectedPumbilityFolders" /> instead.
    /// </summary>
    public async Task<string?> ResolveViewerPeers(ChartType chartType, MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        return (await ResolveViewerPeersAndPool(chartType, mix, userId, cancellationToken)).Key;
    }

    /// <summary>
    ///     The Phoenix 1 reader's peer key and their own pool of the type — the pool the writer
    ///     counted for them, rebuilt from their records with the same rule
    ///     (<see cref="PumbilityPeers.TopPool" />), or null when they hold no full one. Phoenix 1
    ///     keys on the difficulty title regardless; a short pool only means the reader was never
    ///     counted among the members. Phoenix 2 has no stored key and answers null.
    /// </summary>
    private async Task<(string? Key, IReadOnlySet<Guid>? Pool)> ResolveViewerPeersAndPool(ChartType chartType,
        MixEnum mix, Guid userId, CancellationToken cancellationToken)
    {
        if (mix == MixEnum.Phoenix2) return (null, null);
        var pool = await ViewerPool(chartType, mix, userId, cancellationToken);
        var titleLevel = await _titles.GetCurrentTitleLevel(mix, userId, cancellationToken);
        return (PumbilityPeers.ForDifficultyTitleLevel((int)titleLevel), pool);
    }

    /// <summary>
    ///     The Phoenix 1 reader's pool of the type, priced as the nightly job prices everyone's:
    ///     their non-broken records of the type at every level, under the mix's own PUMBILITY
    ///     formula. One player's records and the cached catalog, behind the six-hour blend cache.
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> ViewerPool(ChartType chartType, MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        var records = (await _scores.GetPlayerScoresInLevelRange(mix, new[] { userId }, chartType,
            DifficultyLevel.Min, DifficultyLevel.Max, cancellationToken)).ToArray();
        if (records.Length < PumbilityPeers.PoolSize) return null;

        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        return PumbilityPeers.TopPool(records
            .Where(r => charts.ContainsKey(r.ChartId))
            .Select(r => (r.ChartId,
                scoring.GetScore(charts[r.ChartId], r.Score, r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken))));
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
                projection.MeanFreshness, projection.Group);

        var estimates = projected.ToDictionary(kv => kv.Key, kv => (double)(int)kv.Value);
        return new ProjectionComputation(
            TierListProcessor.ProcessIntoTierList("Projection", estimates)
                .ToDictionary(e => e.ChartId, e => e),
            projected,
            projected.Count,
            folderCharts.Count,
            projection.PeerCount,
            projection.CompetitiveLevel,
            projection.MeanFreshness,
            projection.Group);
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
///     The PUMBILITY source's output: the banded entries, how many of the peer group's pools hold
///     each chart, and how many players the peer group is. An empty read is the honest answer for a
///     folder this peer group's pools cannot reach.
/// </summary>
internal sealed record PumbilityComputation(
    IReadOnlyDictionary<Guid, SongTierListEntry> Entries,
    IReadOnlyDictionary<Guid, int> Appearances,
    int PeerCount);

/// <summary>
///     The projection source's output plus what the page needs in order to say something true
///     when it is quiet: how many of the folder's charts peers at this level have played at all,
///     and the peer group the numbers came from.
/// </summary>
internal sealed record ProjectionComputation(
    IReadOnlyDictionary<Guid, SongTierListEntry> Entries,
    IReadOnlyDictionary<Guid, PhoenixScore> Scores,
    int ProjectedChartCount,
    int FolderChartCount,
    int PeerCount,
    double CompetitiveLevel,
    double MeanFreshness,
    PeerGroup? Peers = null);
