using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>
///     The six counts off an XX result screen. Supplied instead of a score when the play came off
///     an XX cabinet; the domain converts them and applies the chart's note-count adjustment.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record XxJudgements(
    StepCount Perfects,
    StepCount Greats,
    StepCount Goods,
    StepCount Bads,
    StepCount Misses,
    StepCount MaxCombo);
