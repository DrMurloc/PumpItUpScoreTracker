using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.Services.Contracts;

/// <summary>
///     Parses and validates the admin "bulk add charts" JSON blob
///     (schema: docs/design/new-charts-json.md). Pure — no I/O; catalog collision
///     checks stay with the caller.
/// </summary>
public interface IBulkChartJsonParser
{
    BulkChartsParseResult Parse(string json);
}
