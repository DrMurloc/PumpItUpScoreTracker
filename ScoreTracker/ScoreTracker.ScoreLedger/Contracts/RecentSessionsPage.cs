using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     One page of a player's journal, grouped into sessions, newest activity first — ACROSS
///     mixes: the page is one continuous timeline (owner call), each group carrying its mix. A
///     session is a player's plays in one mix until eight hours pass with no play, so it can hold
///     several stored sessions — five imports in an hour are one group (docs/design/session-breakdown.md
///     §8). Rows predating session capture group by calendar day and fold by the same rule.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecentSessionsPage(int TotalGroups, IReadOnlyList<RecentSessionsPage.SessionGroup> Groups)
{
    /// <param name="SessionId">
    ///     The session's handle: the newest stored session in it. Null only when it holds nothing
    ///     but pre-capture days.
    /// </param>
    /// <param name="SessionIds">
    ///     Every stored session folded in, oldest first — what highlights, milestones and the
    ///     session rows join on. A <c>?session=</c> link carrying any one of them opens this group.
    /// </param>
    /// <param name="Day">The newest pre-capture day, for a group with no stored session to name it.</param>
    [ExcludeFromCodeCoverage]
    public sealed record SessionGroup(
        Guid? SessionId,
        IReadOnlyList<Guid> SessionIds,
        DateOnly? Day,
        MixEnum Mix,
        string Source,
        DateTimeOffset Start,
        DateTimeOffset End,
        IReadOnlyList<ScoreEventRecord> Rows);

    /// <summary>
    ///     One play on the page. A stage break carries <see cref="IsStageBroken" /> and no score;
    ///     <see cref="JudgedNotes" /> is how many notes the play judged, when the site's card had
    ///     a breakdown — the row divides it by the chart's note count to say how far the run got.
    ///     <see cref="Judgements" /> is that breakdown itself, when the source carried one —
    ///     null means "not observed", never "all zeroes".
    /// </summary>
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
        // The best passing score in this mix before this play, on every row; null until
        // something has passed.
        int? PreviousBest,
        bool IsReclear = false,
        bool IsStageBroken = false,
        int? JudgedNotes = null,
        JudgementCounts? Judgements = null,
        // Why the stage broke, when the judgement counts could say. IsNonLifebarBreak alone means
        // a Stage Pass command ended the run and we could not name its target
        // (docs/design/pass-command-detection.md D34).
        bool IsNonLifebarBreak = false,
        string? PassPlate = null,
        string? PassGrade = null,
        // The AFK guard ended it — a give-up wearing the 51-miss tail, not a death (D36).
        bool IsWalkOff = false,
        // This play's number among every play of the chart in its mix, itself included: what
        // "Attempt N" prints. It counts what the journal holds.
        int PlayNumber = 0);
}
