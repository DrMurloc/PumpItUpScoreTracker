using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>Charts the sweep scraped but could not match to the catalog — the admin inbox.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetMissingChartsQuery(MixEnum Mix) : IQuery<IReadOnlyList<MissingChartRecord>>;
