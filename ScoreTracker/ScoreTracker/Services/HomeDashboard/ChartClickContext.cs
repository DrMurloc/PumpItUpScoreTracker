using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     The payload of the widget render contract's OnChartClick (§2.2). Widgets that
///     recommend charts stamp the engine's category name so the shared
///     ChartDetailsDialog can offer suggestion feedback (thumbs-up) in context;
///     everything else raises the chart alone.
/// </summary>
public sealed record ChartClickContext(Chart Chart, string? SuggestionCategory = null);
