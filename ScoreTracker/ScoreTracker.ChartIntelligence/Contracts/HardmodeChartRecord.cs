using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     One qualifying chart. <paramref name="Points" /> is the weighted hold count (51 − slot,
///     summed across every full pool the chart sits in) and <paramref name="Holders" /> counts
///     each player once — so a chart three of one player's pools hold carries three slot weights
///     and one holder. <paramref name="FolderSize" /> and <paramref name="FolderCut" /> ride along
///     so a page can say "25 of 170 qualify" without a second read.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeChartRecord(Guid ChartId, ChartType ChartType, int Level, double Points,
    int Holders, int FolderSize, int FolderCut);
