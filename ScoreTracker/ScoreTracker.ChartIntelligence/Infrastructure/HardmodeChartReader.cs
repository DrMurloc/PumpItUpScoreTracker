using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

/// <summary>
///     The Domain port over the same storage the vertical's own handlers read
///     (docs/design/hardmode-leaderboard.md D13) — so a vertical on the wrong side of the
///     reference chain gets the list without a project reference, and gets exactly the list the
///     census wrote rather than its own idea of one.
/// </summary>
internal sealed class HardmodeChartReader : IHardmodeChartReader
{
    private readonly IHardmodeChartRepository _repository;

    public HardmodeChartReader(IHardmodeChartRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<HardmodeChartEntry>> GetQualifyingCharts(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var charts = await _repository.Get(mix, cancellationToken);
        return charts.Select(c => new HardmodeChartEntry(c.ChartId, c.ChartType, c.Level, c.Points, c.Holders,
            c.FolderSize, c.FolderCut)).ToArray();
    }
}
