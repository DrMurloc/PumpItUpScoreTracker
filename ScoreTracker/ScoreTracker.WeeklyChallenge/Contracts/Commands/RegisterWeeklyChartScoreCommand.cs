using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RegisterWeeklyChartScoreCommand(WeeklyTournamentEntry Entry, MixEnum Mix = MixEnum.Phoenix)
    : IRequest
{
}
