using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

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
        string? PassGrade = null);
}
