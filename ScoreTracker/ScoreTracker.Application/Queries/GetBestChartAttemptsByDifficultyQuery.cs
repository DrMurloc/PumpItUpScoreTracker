using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetBestChartAttemptsByDifficultyQuery
    (DifficultyLevel Difficulty) : IRequest<IEnumerable<BestChartAttempt>>
{
}