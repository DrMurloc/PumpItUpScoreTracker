namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One player near you on the competitive ladder: their id and their competitive level
///     on the dimension the lookup ranked by. The caller resolves name/avatar/privacy.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CompetitiveNeighborRecord(Guid UserId, double CompetitiveLevel);
