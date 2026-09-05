using ScoreTracker.Domain.Models;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>One player's full best on a chart, named by account: the row a cross-player chart read returns.</summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerChartRecord(Guid UserId, RecordedPhoenixScore Record);
