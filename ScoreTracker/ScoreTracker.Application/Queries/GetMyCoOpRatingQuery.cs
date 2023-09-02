using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetMyCoOpRatingQuery(Guid ChartId) : IRequest<IDictionary<int, DifficultyLevel>?>
    {
    }
}