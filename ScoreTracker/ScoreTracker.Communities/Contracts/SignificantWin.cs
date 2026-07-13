using System.Diagnostics.CodeAnalysis;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     One community-scoped "big win" in a highlight feed summary
///     (docs/design/home-page-widgets.md §7). Structured, NOT pre-rendered: the row
///     component localizes the caption from these fields — UI strings never ride the DB
///     payload. Persisted as a JSON list in scores.CommunityHighlight and read whole.
///     Field usage per <see cref="WinKind" />:
///     <list type="bullet">
///         <item>BigTitle — TitleName</item>
///         <item>RareTitle — TitleName + RarityShare (holder fraction, e.g. 0.004)</item>
///         <item>FolderComplete — Difficulty (the folder, e.g. "D23") — every chart in it passed</item>
///         <item>FolderFirst — Chart* + Score + Rank (folder ordinal 1/2/3)</item>
///         <item>TopPumbility — Chart* + Score + Rank (pumbility rank, e.g. 2 for #2)</item>
///         <item>PeerElite — Chart* + Score + Rank (peer position, 1 = #1) + RarityShare (top fraction → "top N%")</item>
///         <item>NotablePg — Chart* + Score + RarityShare (fraction of active players holding the PG)</item>
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
    int? Score = null);

public enum WinKind
{
    BigTitle,
    RareTitle,
    FolderComplete,
    FolderFirst,
    TopPumbility,
    PeerElite,
    NotablePg
}

/// <summary>Schema version stamped on every persisted highlight payload — older rows read as stale.</summary>
[ExcludeFromCodeCoverage]
public static class CommunityHighlightSchema
{
    public const int CurrentVersion = 1;
}
