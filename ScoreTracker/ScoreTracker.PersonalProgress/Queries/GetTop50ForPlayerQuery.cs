﻿using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.PersonalProgress.Queries
{
    public sealed record GetTop50ForPlayerQuery
        (Guid UserId, ChartType? ChartType, int Count = 50) : IRequest<IEnumerable<RecordedPhoenixScore>>
    {
    }
}
