using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

/// <summary>
///     Rebuild the PUMBILITY presence census for one mix (docs/design/chart-presence-graph.md §7):
///     for every chart, how many players on each title hold it in their top 50 and where it sits.
///     Published daily by RecurringJobRunner. Phoenix 2 is the only mix with a gem ladder, and a mix
///     nobody stands on writes nothing rather than an empty census.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RebuildChartPresenceCommand(MixEnum Mix);
