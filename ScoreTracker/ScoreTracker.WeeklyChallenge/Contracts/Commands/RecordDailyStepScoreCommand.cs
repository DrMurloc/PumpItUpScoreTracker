using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.WeeklyChallenge.Contracts.Commands;

/// <summary>
///     A manual Daily Step submission from the widget's Record popover. Records ONLY to the daily
///     board — never the score ledger — so a deliberate Limbo low-pass never pollutes a PB, tier
///     list, or Pumbility. The handler resolves the caller server-side and targets the live chart,
///     stamping the entry <see cref="ScoreTracker.Domain.Records.ChallengeEntrySource.Manual" />.
///     Treated as a pass (a selected plate implies completion); no broken/fail path in v1.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecordDailyStepScoreCommand(PhoenixScore Score, PhoenixPlate Plate,
    MixEnum Mix = MixEnum.Phoenix, bool IsBroken = false) : IRequest;
