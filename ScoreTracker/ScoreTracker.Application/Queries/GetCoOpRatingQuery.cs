using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetCoOpRatingQuery(Guid ChartId) : IRequest<CoOpRating?>
    {
    }
}