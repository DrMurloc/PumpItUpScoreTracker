﻿using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartRatingsQuery
    (MixEnum Mix, DifficultyLevel? Level = null, ChartType? Type = null) : IRequest<
        IEnumerable<ChartDifficultyRatingRecord>>
{
}