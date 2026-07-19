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
    int Pumbility,
    int TotalRating,
    int SinglesRating,
    int DoublesRating,
    double CompetitiveLevel,
    double SinglesCompetitiveLevel,
    double DoublesCompetitiveLevel,
    int HighestLevel,
    int ClearCount,
    IReadOnlyList<CommunityFolderCompletionRecord> FolderCompletion);

/// <summary>
///     One level folder: singles and doubles passes separately over the folder's chart total
///     (co-op excluded), so the graph can stack the two types inside the folder's true size.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityFolderCompletionRecord(int Level, int SinglesPassed, int DoublesPassed, int Total)
{
    public int Passed => SinglesPassed + DoublesPassed;
}
