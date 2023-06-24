using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartsBySongQuery(MixEnum Mix, Name SongName) : IRequest<IEnumerable<Chart>>
{
}