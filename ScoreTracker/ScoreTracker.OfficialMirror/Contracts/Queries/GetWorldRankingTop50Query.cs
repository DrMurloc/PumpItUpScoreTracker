using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>A world-ranking player's Top 50 scores for a ranking type ("Singles", "Doubles", …).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWorldRankingTop50Query(Name Username, string Type)
    : IQuery<IEnumerable<RecordedPhoenixScore>>
{
}
