using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record RegisterWeeklyChartScoreCommand(WeeklyTournamentEntry Entry) : IRequest
{
}
