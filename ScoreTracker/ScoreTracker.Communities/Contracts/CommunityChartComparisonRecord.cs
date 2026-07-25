using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     One chart in a you-vs-them folder comparison: both players' best attempts (nulls where
///     unplayed) with recorded dates so the UI can flag stale scores.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityChartComparisonRecord(
    Guid ChartId,
    int? MyScore,
    PhoenixPlate? MyPlate,
    bool MyIsBroken,
    DateTimeOffset? MyRecordedOn,
    int? TheirScore,
    PhoenixPlate? TheirPlate,
    bool TheirIsBroken,
    DateTimeOffset? TheirRecordedOn);
