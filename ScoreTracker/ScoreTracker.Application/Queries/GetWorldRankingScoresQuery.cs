using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

/// <summary>Every score backing a world-ranking player's placement.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWorldRankingScoresQuery(Name Username)
    : IQuery<IEnumerable<RecordedPhoenixScore>>
{
}
