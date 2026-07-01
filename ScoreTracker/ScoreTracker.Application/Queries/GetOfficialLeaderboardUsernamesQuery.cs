namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetOfficialLeaderboardUsernamesQuery : IQuery<IEnumerable<string>>
{
}
