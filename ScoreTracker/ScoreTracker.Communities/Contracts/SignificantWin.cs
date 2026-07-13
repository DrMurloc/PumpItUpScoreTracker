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
///         <item>FolderFirst — Chart* + Rank (folder ordinal 1/2/3)</item>
///         <item>TopPumbility — Chart* + Rank (pumbility rank, e.g. 2 for #2)</item>
///         <item>PeerElite — Chart* + Rank ("top N%" among the ±0.5 cohort)</item>
///         <item>NotablePg — Chart* + RarityShare (fraction of active players holding the PG)</item>
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
    int? Rank = null);

public enum WinKind
{
    BigTitle,
    RareTitle,
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
