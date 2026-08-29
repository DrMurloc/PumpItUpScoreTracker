namespace ScoreTracker.ScoreLedger.Contracts.Messages;

/// <summary>
///     Re-solves what ended every judged stage break, one player at a time, from the play's five
///     judgement counts, the catalog's note count and level, and the mix's grade floors
///     (docs/design/pass-command-detection.md). Every judged stage break is re-derived, not only
///     the unclassified ones: the solver is the moving part, and a corrected note count or a
///     sharper rule catches up on the next press. Idempotent; the /Admin button publishes it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BackfillStageBreakCausesCommand;
