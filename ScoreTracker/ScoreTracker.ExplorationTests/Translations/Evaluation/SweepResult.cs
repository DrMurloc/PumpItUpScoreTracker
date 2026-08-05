using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.ExplorationTests.Translations.Evaluation;

/// <summary>
///     What one arm produced for one comment, or why it did not. A failure is recorded rather
///     than thrown so one bad response does not discard the other twenty-two results — and so a
///     model that fails on a particular comment is visible as a row in the report instead of a
///     stack trace that ends the run.
/// </summary>
internal sealed record SweepResult(
    ModelArm Arm,
    CorpusComment Comment,
    TranslationOutcome? Outcome,
    string? Failure)
{
    public decimal Cost => Outcome?.Calls.Sum(c => Arm.Cost(c.Usage)) ?? 0m;

    public bool DetectedLanguageCorrectly =>
        Outcome != null &&
        Outcome.Pivot.SourceLanguage.StartsWith(Comment.ExpectedLanguage, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<EntityFinding> Entities =>
        Outcome == null ? [] : EntityPreservationCheck.Check(Comment, Outcome);
}
