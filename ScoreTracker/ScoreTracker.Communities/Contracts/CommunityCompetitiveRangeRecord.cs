using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>A community's qualifying competitive-level spread; a null side means no qualifying players.</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityCompetitiveRangeRecord(
    Name CommunityName,
    double? SinglesMin,
    double? SinglesMax,
    double? DoublesMin,
    double? DoublesMax);
