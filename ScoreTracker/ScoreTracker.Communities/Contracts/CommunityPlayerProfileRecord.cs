using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     The community player page's summary: identity, the headline ratings, and per-level
///     folder completion. Visible to anyone who can see the community's boards — joining a
///     community is the score-visibility consent.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityPlayerProfileRecord(
    Guid UserId,
    Name PlayerName,
    Uri ProfileImage,
    Name? Country,
    bool IsPublic,
    double Pumbility,
    int TotalRating,
    double SinglesRating,
    double DoublesRating,
    double CompetitiveLevel,
    double SinglesCompetitiveLevel,
    double DoublesCompetitiveLevel,
    int HighestLevel,
    int ClearCount,
    IReadOnlyList<CommunityFolderCompletionRecord> FolderCompletion);

/// <summary>
///     One folder — a (chart type, level) pair, since S18 and D18 are different folders with
///     different levels (docs/design/folder-level-progression.md §2.4). Co-op stays out: its
///     "levels" are player counts, which do not belong on a difficulty axis.
///     <para>
///         <see cref="GradeCounts" /> is the folder's passes bucketed by grade, which is what lets
///         the column render as a spectrum rather than a flat bar. Passes only — a broken score is
///         a failed run and feeds nothing here.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityFolderCompletionRecord(
    ChartType Type,
    int Level,
    int Passed,
    int Total,
    IReadOnlyDictionary<PhoenixLetterGrade, int> GradeCounts);
