using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Views;

public sealed record MatchView(Name MatchName, Name RandomSettings, MatchState State, Name[] Players,
    Guid[] ActiveCharts,
    Guid[] VetoedCharts, Guid[] ProtectedCharts, IDictionary<string, PhoenixScore[]> Scores,
    IDictionary<string, int[]> Points, Name[] FinalPlaces)
{
}