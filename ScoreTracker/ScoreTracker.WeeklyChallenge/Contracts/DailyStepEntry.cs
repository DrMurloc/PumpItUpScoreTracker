using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     A player's entry on today's Daily Step chart, with its provenance so the board can flag
///     verified official-import scores apart from self-reported manual ones.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DailyStepEntry(Guid UserId, Guid ChartId, PhoenixScore Score, PhoenixPlate Plate,
    bool IsBroken, double CompetitiveLevel, ChallengeEntrySource Source);
