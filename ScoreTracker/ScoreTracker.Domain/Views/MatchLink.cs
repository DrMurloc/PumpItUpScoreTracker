using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Views
{
    public sealed record MatchLink(Name FromMatch, Name ToMatch, bool IsWinners, int PlayerCount)
    {
    }
}
