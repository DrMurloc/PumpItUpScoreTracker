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

    /// <summary>A title held by fewer than this fraction of titled players is a rare title.</summary>
    public const double TitleRarityThreshold = 0.01;

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
    private const string DifficultyCategory = "Difficulty";

    // Difficulty titles (Phoenix) and PUMBILITY titles (Phoenix 2) both carry Category "Difficulty" —
    // exactly the owner's "big title" set. Names come from the shipped title taxonomy.
    private static readonly IReadOnlySet<string> PhoenixDifficultyTitles = DifficultyTitleNames(PhoenixTitleList.BuildList());
    private static readonly IReadOnlySet<string> Phoenix2DifficultyTitles = DifficultyTitleNames(Phoenix2TitleList.BuildList());

    public static IReadOnlyList<SignificantWin> Classify(ScoreHighlightsCapturedEvent e,
        IReadOnlyDictionary<Guid, Chart> charts, RaritySnapshot snapshot, PlayerStatsRecord stats)
    {
        var wins = new List<(int Priority, SignificantWin Win)>();

        foreach (var milestone in e.Milestones)
        {
            var win = ClassifyMilestone(milestone, e.Mix, snapshot);
            if (win is not null) wins.Add(win.Value);
        }

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

    // A title completion or full-folder clear → its win. FolderPassLamp Detail is the folder ("D23");
    // titles split into difficulty/pumbility "big" titles and sub-1%-held "rare" ones.
    private static (int Priority, SignificantWin Win)? ClassifyMilestone(PlayerMilestoneRecord milestone,
        MixEnum mix, RaritySnapshot snapshot)
    {
        if (milestone.Kind == MilestoneKind.FolderPassLamp && milestone.Detail is { Length: > 0 } folder)
            return (PriorityFolderComplete, new SignificantWin(WinKind.FolderComplete, Difficulty: folder));

        if (milestone.Kind == MilestoneKind.FolderProgress) return ClassifyFolderProgress(milestone);

        if (milestone.Kind != MilestoneKind.TitleCompleted || milestone.Title is null) return null;
        var title = milestone.Title;
        if (IsBigTitle(mix, title))
            return (PriorityBigTitle, new SignificantWin(WinKind.BigTitle, TitleName: title));
        if (TitleShare(title, snapshot) is { } share && share < TitleRarityThreshold)
            return (PriorityRareTitle, new SignificantWin(WinKind.RareTitle, TitleName: title, RarityShare: share));
        return null;
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
            && (int)chart.Level >= FlooredCompetitiveLevel(chart.Type, stats))
            return (PriorityFolderFirst, Win(WinKind.FolderFirst, chart, change.NewScore, rank: ordinal));

        return null;
    }

    // A (type, level) folder is gated by the competitive level for its own discipline, falling back
    // to the overall level for non-Singles/Doubles folders.
    private static int FlooredCompetitiveLevel(ChartType type, PlayerStatsRecord stats) =>
        (int)Math.Floor(type switch
        {
            ChartType.Single => stats.SinglesCompetitiveLevel,
            ChartType.Double => stats.DoublesCompetitiveLevel,
            _ => stats.CompetitiveLevel
        });

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

    private static double? TitleShare(string title, RaritySnapshot snapshot) =>
        snapshot.TitledUserCount > 0 && snapshot.TitleHoldersByName.TryGetValue(title, out var holders)
            ? holders / (double)snapshot.TitledUserCount
            : null;

    private static bool IsBigTitle(MixEnum mix, string titleName) => mix == MixEnum.Phoenix2
        ? Phoenix2DifficultyTitles.Contains(titleName)
        : PhoenixDifficultyTitles.Contains(titleName);

    private static IReadOnlySet<string> DifficultyTitleNames(IEnumerable<PhoenixTitle> titles) =>
        titles.Where(t => string.Equals(t.Category.ToString(), DifficultyCategory, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Name.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Priority: lower renders first (owner order 2026-07-13): titles, then folder wins, then the
    // number wins. Within titles, rare above big.
    private const int PriorityRareTitle = 0;
    private const int PriorityBigTitle = 1;
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
/// </summary>
internal sealed record RaritySnapshot(
    IReadOnlyDictionary<Guid, int> PgHoldersByChart,
    int ActivePlayerCount,
    IReadOnlyDictionary<string, int> TitleHoldersByName,
    int TitledUserCount);
