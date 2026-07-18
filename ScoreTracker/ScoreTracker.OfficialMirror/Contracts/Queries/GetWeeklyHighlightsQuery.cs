using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>The week's editorial board from the latest sealed snapshot; null when none exists.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWeeklyHighlightsQuery(MixEnum Mix) : IQuery<WeeklyHighlightsRecord?>;
