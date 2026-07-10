using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>Every score backing a world-ranking player's placement.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWorldRankingScoresQuery(Name Username, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<RecordedPhoenixScore>>
{
}
