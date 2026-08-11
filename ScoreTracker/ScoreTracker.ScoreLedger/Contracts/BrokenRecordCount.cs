using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     How many of a player's records on one mix are failed runs rather than passes.
///     <para>
///         Legacy mixes never appear: they record a letter grade in <c>BestAttempt</c>, which has
///         no notion of a failed stage, so there is nothing there to count or to clean up.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BrokenRecordCount(MixEnum Mix, int Count);
