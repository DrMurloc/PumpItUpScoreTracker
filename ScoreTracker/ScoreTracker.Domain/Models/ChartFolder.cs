using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed record ChartFolder(Name Name, IOrderedEnumerable<Chart> Charts, ChartType? ChartType,
    DifficultyLevel? Level)
{
}