using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Views
{
    public sealed record MatchLink(Guid Id, Name FromMatch, Name ToMatch, bool IsWinners, int PlayerCount, int Skip)
    {
    }
}
