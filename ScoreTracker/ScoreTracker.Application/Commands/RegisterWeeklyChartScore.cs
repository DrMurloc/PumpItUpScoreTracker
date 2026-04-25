using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record RegisterWeeklyChartScore(WeeklyTournamentEntry Entry) : IRequest
{
}
