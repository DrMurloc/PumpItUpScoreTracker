using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     One page of a player's journal, grouped into sessions (or, for rows predating
///     session capture, calendar days), newest activity first — ACROSS mixes: the page
///     is one continuous timeline (owner call), each group carrying its mix.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecentSessionsPage(int TotalGroups, IReadOnlyList<RecentSessionsPage.SessionGroup> Groups)
{
    [ExcludeFromCodeCoverage]
    public sealed record SessionGroup(
        Guid? SessionId,
        DateOnly? Day,
        MixEnum Mix,
        string Source,
        DateTimeOffset Start,
        DateTimeOffset End,
        IReadOnlyList<ScoreEventRecord> Rows);

    [ExcludeFromCodeCoverage]
    public sealed record ScoreEventRecord(
        Guid ChartId,
        DateTimeOffset OccurredAt,
        int? Score,
        string? Plate,
        bool IsBroken,
        string Source,
        Guid? SessionId,
        ScoreEventClassification Classification,
        int? PreviousBest,
        // A NewPass on a newer version (e.g. first Phoenix 2 pass) carries the player's best
        // on an earlier same-scale version, so the row can show "+X from <PreviousMix>".
        // Null unless the row is a cross-version first pass with an earlier-version score.
        int? PreviousMixBest = null,
        MixEnum? PreviousMix = null);
}
