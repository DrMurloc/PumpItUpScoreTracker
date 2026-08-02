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
///         <item>BigTitle — TitleName</item>
///         <item>RareTitle — TitleName + RarityShare (holder fraction, e.g. 0.004)</item>
///         <item>FolderComplete — Difficulty (the folder, e.g. "D23") — every chart in it passed</item>
///         <item>FolderFirst — Chart* + Score + Rank (folder ordinal 1/2/3)</item>
///         <item>TopPumbility — Chart* + Score + Rank (pumbility rank, e.g. 2 for #2)</item>
///         <item>PeerElite — Chart* + Score + Rank (peer position, 1 = #1) + RarityShare (top fraction → "top N%")</item>
///         <item>NotablePg — Chart* + Score + RarityShare (fraction of active players holding the PG)</item>
///         <item>FolderProgress — Difficulty (the folder) + Rank (completion tier) + Detail (the grade)</item>
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
    string? Detail = null);

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
    FolderProgress
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
/// </summary>
[ExcludeFromCodeCoverage]
public static class PlayerHighlightSchema
{
    public const int CurrentVersion = 2;
}
