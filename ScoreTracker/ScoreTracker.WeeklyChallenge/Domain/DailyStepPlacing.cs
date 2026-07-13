using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.WeeklyChallenge.Domain;

/// <summary>
///     A finished Daily Step placement, snapshotted into <c>UserDailyStepPlacing</c> when the board
///     rotates. <c>IsLimbo</c> rides along so a later view can render the finishing board's rules
///     correctly. Mirrors <see cref="ScoreTracker.Domain.Records.UserTourneyHistory" /> for Weekly.
/// </summary>
internal sealed record DailyStepPlacing(Guid UserId, Guid ChartId, DateTimeOffset ForDate, bool IsLimbo, int Place,
    PhoenixScore Score, PhoenixPlate Plate, bool IsBroken, double CompetitiveLevel);
