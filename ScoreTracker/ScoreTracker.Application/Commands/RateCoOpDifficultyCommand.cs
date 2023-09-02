using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record RateCoOpDifficultyCommand
        (MixEnum mix, Guid ChartId, IDictionary<int, DifficultyLevel>? Ratings) : IRequest<CoOpRating?>
    {
    }
}