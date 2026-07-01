namespace ScoreTracker.Application.Queries;

/// <summary>Official-site avatar per known leaderboard username.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetUserAvatarsQuery : IQuery<IEnumerable<(string Username, Uri AvatarPath)>>
{
}
