using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     Decides which of a score batch's changes/milestones are BIG wins
///     (docs/design/home-page-widgets.md §7). Pure — the capturer loads the population snapshots
///     (cached) and passes them in, so every cutoff is pinned by DomainTest and tunable without
///     touching plumbing.
///     <para>
///         This used to live in Communities on the reasoning that significance was a community
///         judgment. Every input below says otherwise: a site-wide rarity snapshot and the
///         player's own stats, with no community anywhere. The audience is what varies — a
///         community's members, or somebody's rival list — so the bar itself belongs here, once
///         (docs/design/rivals.md D31).
///     </para>
/// </summary>
internal static class PlayerHighlightPolicy
{
    // ── Cutoffs (owner 2026-07-12). Higher bars than the per-player highlight flags: those
    //    are tier one, the Sessions page's own material; these are tier two, what a feed shows
    //    other people. ──
    /// <summary>A PG fewer than this fraction of active players hold is notable.</summary>
    public const double PgRarityThreshold = 0.01;

    /// <summary>PG rarity self-selects hard charts, but a new easy chart reads "rare" too — floor it.</summary>
    public const int PgMinLevel = 20;

    /// <summary>A pumbility rank at or above this (i.e. ≤ N) is a huge pumbility win.</summary>
    public const int PumbilityTopRank = 10;

    /// <summary>Among the first N passes ever in a folder.</summary>
    public const int FolderFirstMaxOrdinal = 3;

    /// <summary>
    ///     Only deep completion reads as a community win — 20% and 40% are personal progress, and
    ///     the Discord card already carries them (docs/design/folder-level-progression.md §5.5).
    /// </summary>
    public const int FolderTierMinPercent = 60;

    /// <summary>A folder grade improvement counts only from S upward.</summary>
    public const PhoenixLetterGrade FolderGradeMin = PhoenixLetterGrade.S;

    /// <summary>Top this fraction of the ±0.5 competitive cohort (i.e. &gt; 95th percentile).</summary>
    public const double PeerEliteFraction = 0.05;

    /// <summary>Below this cohort size, "top 5%" is noise.</summary>
    public const int PeerEliteMinCohort = 10;

    /// <summary>A summary is a summary — the most impressive few, not a wall.</summary>
    public const int MaxWinsPerEvent = 4;

    private const string PerfectGamePlate = "Perfect Game";
    private const string DefaultTitleDescription = "Default title";

    // Name → rung for the three Phoenix 2 PUMBILITY pool ladders ([S], [D], [P.B] total) — the
    // only titles that roll up (owner, 2026-08-14: "specifically only the pumbility titles").
    // Phoenix 1 has no pool ladders; its difficulty titles print one row per rung like every
    // other family.
    private static readonly IReadOnlyDictionary<string, Phoenix2PumbilityTitle> Phoenix2PumbilityRungs =
        Phoenix2TitleList.BuildList().OfType<Phoenix2PumbilityTitle>()
            .ToDictionary(t => t.Name.ToString(), t => t, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, Phoenix2PumbilityTitle> NoPumbilityRungs =
        new Dictionary<string, Phoenix2PumbilityTitle>(StringComparer.OrdinalIgnoreCase);

    // The default title every account starts with — a first import "earning" it is noise.
    private static readonly IReadOnlySet<string> DefaultTitleNames = PhoenixTitleList.BuildList()
        .Concat(Phoenix2TitleList.BuildList())
        .Where(t => string.Equals(t.Description, DefaultTitleDescription, StringComparison.OrdinalIgnoreCase))
        .Select(t => t.Name.ToString())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SignificantWin> Classify(ScoreHighlightsCapturedEvent e,
        IReadOnlyDictionary<Guid, Chart> charts, RaritySnapshot snapshot, PlayerStatsRecord stats)
    {
        var wins = new List<(int Priority, SignificantWin Win)>();

        foreach (var milestone in e.Milestones)
        {
            var win = ClassifyMilestone(milestone);
            if (win is not null) wins.Add(win.Value);
        }

        wins.AddRange(ClassifyTitles(e.Mix, e.Milestones));

        // The level crossing is a batch-level fact, not a per-milestone one: whether it speaks
        // depends on which titles completed beside it, so it derives from the whole set.
        if (PumbilityLevelChange.TryFrom(e.Mix, e.Milestones) is { } levelUp)
            wins.Add((PriorityPumbilityLevelUp, new SignificantWin(WinKind.PumbilityLevelUp,
                Rank: levelUp.To.Index, PoolValue: levelUp.NewPool)));

        foreach (var change in e.Changes)
        {
            if (!charts.TryGetValue(change.ChartId, out var chart)) continue;
            var win = ClassifyChange(change, chart, snapshot, stats);
            if (win is not null) wins.Add(win.Value);
        }

        return wins
            .OrderBy(w => w.Priority)
            .ThenBy(w => w.Win.RarityShare ?? 1.0)
            .ThenBy(w => w.Win.Rank ?? int.MaxValue)
            .Take(MaxWinsPerEvent)
            .Select(w => w.Win)
            .ToArray();
    }

    // A full-folder clear or folder movement → its win. FolderPassLamp Detail is the folder ("D23").
    private static (int Priority, SignificantWin Win)? ClassifyMilestone(PlayerMilestoneRecord milestone)
    {
        if (milestone.Kind == MilestoneKind.FolderPassLamp && milestone.Detail is { Length: > 0 } folder)
            return (PriorityFolderComplete, new SignificantWin(WinKind.FolderComplete, Difficulty: folder));

        if (milestone.Kind == MilestoneKind.FolderProgress) return ClassifyFolderProgress(milestone);

        return null;
    }

    /// <summary>
    ///     Every earned title is feed-worthy (owner, 2026-08-14: "all titles are big titles") — the
    ///     old big/rare gate is gone, and with it the policy's only use of title-rarity data. The one
    ///     grouping: a batch that climbs several rungs of one Phoenix 2 PUMBILITY pool ladder reads
    ///     as a single span ("[S] ADVANCED LV.6 → LV.9") rather than one row per rung — in the feeds
    ///     only, since the Discord card renders from the milestones themselves and stays loud.
    /// </summary>
    private static IEnumerable<(int Priority, SignificantWin Win)> ClassifyTitles(MixEnum mix,
        IReadOnlyList<PlayerMilestoneRecord> milestones)
    {
        var completed = milestones
            .Where(m => m.Kind == MilestoneKind.TitleCompleted && m.Title is { Length: > 0 })
            .Select(m => m.Title!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t => !DefaultTitleNames.Contains(t))
            .ToArray();

        var rungs = mix == MixEnum.Phoenix2 ? Phoenix2PumbilityRungs : NoPumbilityRungs;

        // The ladder story leads: one span per pool whose rungs order by threshold, a lone rung
        // staying a plain title row.
        foreach (var pool in completed.Where(rungs.ContainsKey).GroupBy(t => rungs[t].Pool))
        {
            var climbed = pool.OrderBy(t => rungs[t].CompletionRequired).ToArray();
            yield return climbed.Length == 1
                ? (PriorityTitle, new SignificantWin(WinKind.BigTitle, TitleName: climbed[0]))
                : (PriorityTitle, new SignificantWin(WinKind.PumbilityTitleSpan,
                    TitleName: climbed[^1], Detail: climbed[0]));
        }

        foreach (var title in completed.Where(t => !rungs.ContainsKey(t)))
            yield return (PriorityTitle, new SignificantWin(WinKind.BigTitle, TitleName: title));
    }

    /// <summary>
    ///     A folder movement earns a community slot two ways: reaching a deep completion tier, or
    ///     climbing the grade into the top band. Either alone is enough, but shallow tiers and
    ///     sub-S grades stay off the feed — those already ride the Discord card in full.
    /// </summary>
    private static (int Priority, SignificantWin Win)? ClassifyFolderProgress(PlayerMilestoneRecord milestone)
    {
        var detail = FolderProgressDetail.TryParse(milestone.Detail);
        if (detail == null) return null;

        var deepTier = detail.TierMoved && detail.Tier >= FolderTierMinPercent;
        var topGrade = detail.GradeMoved && detail.Grade >= FolderGradeMin;
        if (!deepTier && !topGrade) return null;

        // A lamp already has its own FolderComplete win, so this would double the row.
        if (detail.IsLamp) return null;

        // Which half is the news decides how the row reads, and a deep tier outranks a grade
        // climb. Detail carries the grade only when the grade is the story, so the renderer can
        // pick its sentence without a second flag.
        return (PriorityFolderProgress, new SignificantWin(WinKind.FolderProgress,
            Difficulty: detail.Folder, Rank: detail.Tier,
            Detail: deepTier ? null : detail.Grade?.GetName()));
    }

    private static (int Priority, SignificantWin Win)? ClassifyChange(
        ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart, RaritySnapshot snapshot,
        PlayerStatsRecord stats)
    {
        // A PG routes to the sitewide-rarity track only (never doubled as a peer-elite score).
        if (IsPerfectGame(change) && !change.IsBroken && (int)chart.Level >= PgMinLevel)
        {
            var share = PgShare(chart.Id, snapshot);
            return share < PgRarityThreshold
                ? (PriorityNotablePg, Win(WinKind.NotablePg, chart, change.NewScore, rarityShare: share))
                : null;
        }

        if (change.Flags.HasFlag(HighlightFlags.PumbilityTop50)
            && change.Detail?.PumbilityRank is { } rank && rank <= PumbilityTopRank)
            return (PriorityTopPumbility, Win(WinKind.TopPumbility, chart, change.NewScore, rank: rank));

        if (change.Flags.HasFlag(HighlightFlags.ScoreQuality90)
            && change.Detail is { PeerCount: >= PeerEliteMinCohort } detail
            && (detail.PeerBetterCount ?? 0) / (double)detail.PeerCount!.Value <= PeerEliteFraction)
        {
            // Rank = peer position (1 = nobody beat you); RarityShare = the top fraction the widget
            // turns into "top N%". Position 1 renders as "#1 of all peers", never "top 0%".
            var position = (detail.PeerBetterCount ?? 0) + 1;
            return (PriorityPeerElite, Win(WinKind.PeerElite, chart, change.NewScore,
                rarityShare: position / (double)detail.PeerCount!.Value, rank: position));
        }

        // A folder debut only counts at or above the player's floored competitive level for that
        // discipline — an early pass in a folder well below your skill isn't a community big win.
        if (change.Flags.HasFlag(HighlightFlags.FolderDebut)
            && change.Detail?.FolderDebutOrdinal is { } ordinal && ordinal <= FolderFirstMaxOrdinal
            && (int)chart.Level >= CompetitiveLevels.Floor(chart.Type, stats))
            return (PriorityFolderFirst, Win(WinKind.FolderFirst, chart, change.NewScore, rank: ordinal));

        return null;
    }

    private static SignificantWin Win(WinKind kind, Chart chart, int? score, double? rarityShare = null,
        int? rank = null) =>
        new(kind, ChartId: chart.Id, ChartName: chart.Song.Name.ToString(), Difficulty: chart.DifficultyString,
            RarityShare: rarityShare, Rank: rank, Score: score);

    private static bool IsPerfectGame(ScoreHighlightsCapturedEvent.HighlightedChange change) =>
        string.Equals(change.Plate, PerfectGamePlate, StringComparison.OrdinalIgnoreCase);

    private static double PgShare(Guid chartId, RaritySnapshot snapshot) =>
        snapshot.ActivePlayerCount <= 0
            ? 0
            : snapshot.PgHoldersByChart.GetValueOrDefault(chartId) / (double)snapshot.ActivePlayerCount;

    // Priority: lower renders first (owner order 2026-07-13): titles, then folder wins, then the
    // number wins. The level crossing sits with the titles — it is a gem sub-step, and it only
    // ever speaks when the gem title itself stayed quiet.
    private const int PriorityTitle = 0;
    private const int PriorityPumbilityLevelUp = 1;
    private const int PriorityFolderComplete = 2;
    private const int PriorityFolderProgress = 3;
    private const int PriorityFolderFirst = 4;
    private const int PriorityTopPumbility = 5;
    private const int PriorityNotablePg = 6;
    private const int PriorityPeerElite = 7;
}

/// <summary>
///     The slow-moving population aggregates the policy needs, snapshotted so the busy import path
///     doesn't recompute them per event. Loaded by the capturer behind a per-mix memory cache.
///     Title-holder aggregates left 2026-08-14 with the rare-title gate — PG rarity is the one
///     population read left.
/// </summary>
internal sealed record RaritySnapshot(
    IReadOnlyDictionary<Guid, int> PgHoldersByChart,
    int ActivePlayerCount);
