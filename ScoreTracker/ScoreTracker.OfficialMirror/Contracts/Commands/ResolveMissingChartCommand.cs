using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

/// <summary>
///     Clears one missing-chart inbox row after the admin handles it (usually by adding
///     the chart to the catalog). If the gap is still real, the next sweep re-lists it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ResolveMissingChartCommand(int Id) : IRequest;
