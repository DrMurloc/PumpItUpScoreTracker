using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.WeeklyChallenge.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RegisterWeeklyChartScoreCommand(WeeklyTournamentEntry Entry) : IRequest
{
}
