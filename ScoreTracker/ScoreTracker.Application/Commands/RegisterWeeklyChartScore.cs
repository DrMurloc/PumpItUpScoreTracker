using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

public sealed record RegisterWeeklyChartScore(WeeklyTournamentEntry Entry) : IRequest
{
}