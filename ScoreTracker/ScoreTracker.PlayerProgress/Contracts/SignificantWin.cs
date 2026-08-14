using System.Diagnostics.CodeAnalysis;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One "big win" in a highlight feed summary. Structured, NOT pre-rendered: the row
///     component localizes the caption from these fields — UI strings never ride the DB
///     payload. Persisted as a JSON list in scores.PlayerHighlight and read whole.
///     <para>
///         This vocabulary used to live in Communities, on the reasoning that significance was a
///         community judgment. It isn't: the policy that produces these takes a SITE-WIDE rarity
///         snapshot plus the player's own stats and never looks at a community. A community is
///         only ever the audience — and now so is a rival list, which is what moved the type here
///         (docs/design/rivals.md D31–D32).
///     </para>
///     Field usage per <see cref="WinKind" />:
///     <list type="bullet">
///         <item>BigTitle — TitleName (since 2026-08-14: ANY earned title — the big/rare split retired)</item>
///         <item>PumbilityTitleSpan — TitleName (the rung reached) + Detail (the first rung crossed)</item>
///         <item>RareTitle — TitleName + RarityShare; no longer produced, survives on stored rows</item>
///         <item>FolderComplete — Difficulty (the folder, e.g. "D23") — every chart in it passed</item>
///         <item>FolderFirst — Chart* + Score + Rank (folder ordinal 1/2/3)</item>
///         <item>TopPumbility — Chart* + Score + Rank (pumbility rank, e.g. 2 for #2)</item>
///         <item>PeerElite — Chart* + Score + Rank (peer position, 1 = #1) + RarityShare (top fraction → "top N%")</item>
///         <item>NotablePg — Chart* + Score + RarityShare (fraction of active players holding the PG)</item>
///         <item>FolderProgress — Difficulty (the folder) + Rank (completion tier) + Detail (the grade)</item>
///         <item>PumbilityLevelUp — Rank (the badge index reached, 1–36) + PoolValue (the new pool)</item>
///     </list>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SignificantWin(
    WinKind Kind,
    Guid? ChartId = null,
    string? ChartName = null,
    string? Difficulty = null,
    string? TitleName = null,
    double? RarityShare = null,
    int? Rank = null,
    int? Score = null,
    string? Detail = null,
    double? PoolValue = null);

public enum WinKind
{
    BigTitle,
    RareTitle,
    FolderComplete,
    FolderFirst,
    TopPumbility,
    PeerElite,
    NotablePg,

    /// <summary>
    ///     A folder reached a deep completion tier, or its grade climbed into the top band.
    ///     Narrower than the Discord card on purpose (docs/design/folder-level-progression.md §5.5).
    /// </summary>
    FolderProgress,

    /// <summary>
    ///     The player crossed a PUMBILITY level — a rung inside a [P.B] gem
    ///     (docs/design/pumbility-levels.md §5). Suppressed at classification when the same batch
    ///     completed the gem title itself, so this row only speaks when the title didn't.
    /// </summary>
    PumbilityLevelUp,

    /// <summary>
    ///     One batch climbed several rungs of a single Phoenix 2 PUMBILITY pool ladder, and the
    ///     rungs roll into one row ("[S] ADVANCED LV.6 → LV.9") instead of one per rung — in the
    ///     feeds only; the Discord card renders from the milestones themselves and stays
    ///     uncollapsed (owner, 2026-08-14). TitleName is the rung reached, Detail the first rung
    ///     crossed. Only the pumbility ladders roll up; every other title family prints per rung.
    /// </summary>
    PumbilityTitleSpan
}

/// <summary>
///     Schema version stamped on every persisted highlight payload — older rows read as stale.
///     <para>
///         v2 added <see cref="SignificantWin.Detail" /> and <see cref="WinKind.FolderProgress" />.
///         v1 rows deserialize cleanly (the new field is optional and lands null) but are treated
///         as stale rather than rendered, which is the point of the stamp: a feed row is a summary
///         of a moment, and a moment summarised before folder standings existed is incomplete
///         rather than wrong. The weekly purge clears them inside 30 days.
///     </para>
///     <para>
///         Version 2 carries over unchanged from the Communities-owned era: the payload's JSON
///         shape did not move when the type's namespace did, so stored rows read as current.
///     </para>
///     <para>
///         v3 added <see cref="WinKind.PumbilityLevelUp" /> and <see cref="SignificantWin.PoolValue" />
///         — same reasoning as v2: a summary written before level crossings existed is incomplete
///         rather than wrong, and rows regenerate on their next import.
///     </para>
///     <para>
///         2026-08-14: <see cref="WinKind.PumbilityTitleSpan" /> and the all-titles inclusion landed
///         WITHOUT a bump — deliberately. A pre-change row is a complete summary under the rules of
///         its day (nothing it should have carried is missing), so it keeps rendering; the 30-day
///         window ages the old shapes out on its own.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class PlayerHighlightSchema
{
    public const int CurrentVersion = 3;
}
