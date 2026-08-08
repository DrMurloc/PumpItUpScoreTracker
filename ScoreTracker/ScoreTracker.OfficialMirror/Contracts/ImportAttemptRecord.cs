using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     One import attempt as the Import Scores page reads it.
///     <para>
///         <paramref name="FinishedAt" /> and <paramref name="Outcome" /> are null together, and
///         that pair is a state rather than missing data: the run never reported back. Callers
///         must render it as its own thing — treating it as a failure blames a site that may have
///         been fine, and treating it as a success claims scores that were never read.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportAttemptRecord(
    Guid Id,
    MixEnum Mix,
    ImportKind Kind,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    ImportOutcome? Outcome,
    Guid? SessionId,
    int? ScoreCount)
{
    /// <summary>How long the run took, or null while it is still open.</summary>
    public TimeSpan? Duration => FinishedAt - StartedAt;

    /// <summary>
    ///     A run that got as far as opening a session but never closed. Reported separately from
    ///     both success and failure — see the note on <see cref="Outcome" />'s nullability.
    /// </summary>
    public bool NeverFinished => FinishedAt is null;
}
