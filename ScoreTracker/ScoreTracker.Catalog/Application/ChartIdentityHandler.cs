using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The one place a chart's identity is decided (docs/design/chart-identity.md §6). Reads
///     the banked step analysis, places each chart in its folder for the requested mix, and
///     hands the folder's baselines to the chip rules.
///     <para>
///         Also serves the archived hand tags, which are the same question asked of the
///         historical record: they answer for the Chabala lens and nothing else.
///     </para>
/// </summary>
internal sealed class ChartIdentityHandler
    : IRequestHandler<GetChartIdentityQuery, IReadOnlyDictionary<Guid, ChartIdentityRecord>>,
        IRequestHandler<GetChartBadgePresenceQuery, IReadOnlyDictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>>,
        IRequestHandler<GetArchivedSkillTagsQuery, IReadOnlyDictionary<Guid, IReadOnlyList<string>>>
{
    private readonly IArchivedSkillTagRepository _archive;
    private readonly IChartFolderBaselineRepository _baselines;
    private readonly IChartRepository _charts;
    private readonly IChartSkillMetricRepository _metrics;

    public ChartIdentityHandler(IChartSkillMetricRepository metrics, IChartRepository charts,
        IChartFolderBaselineRepository baselines, IArchivedSkillTagRepository archive)
    {
        _metrics = metrics;
        _charts = charts;
        _baselines = baselines;
        _archive = archive;
    }

    public async Task<IReadOnlyDictionary<Guid, ChartIdentityRecord>> Handle(GetChartIdentityQuery request,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, ChartIdentityRecord>();
        if (request.ChartIds.Count == 0) return result;

        var metrics = await _metrics.GetMetrics(request.ChartIds, PiuCenterMetrics.Source, cancellationToken);
        if (metrics.Count == 0) return result;

        // The mix's catalog places each chart in a folder — the level is the part that moves
        // between mixes, and it is what the baselines are keyed by.
        var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        foreach (var group in metrics.GroupBy(m => m.ChartId))
        {
            if (!charts.TryGetValue(group.Key, out var chart)) continue;
            var folder = await _baselines.GetFolderBaselines(request.Mix, chart.Type, (int)chart.Level,
                cancellationToken);
            var chips = ChartIdentityBuilder.Build(ChartBadgeProfile.From(group.Key, group.ToArray()), folder);
            if (chips.Count > 0) result[group.Key] = new ChartIdentityRecord(group.Key, chips);
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>> Handle(
        GetChartBadgePresenceQuery request, CancellationToken cancellationToken)
    {
        var metrics = await _metrics.GetMetrics(request.ChartIds, PiuCenterMetrics.Source, cancellationToken);
        // Presence is folder-relative now, so this walks folders the same way identity does.
        var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var result = new Dictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>();
        foreach (var group in metrics.GroupBy(m => m.ChartId))
        {
            if (!charts.TryGetValue(group.Key, out var chart)) continue;
            var folder = await _baselines.GetFolderBaselines(request.Mix, chart.Type, (int)chart.Level,
                cancellationToken);
            var profile = ChartBadgeProfile.From(group.Key, group.ToArray());
            var badges = profile.PresentBadges(folder)
                .Select(b => new ChartBadgePresenceRecord(b, BadgeLabels.DisplayName(b),
                    BadgeLabels.CategoryFor(b),
                    ChartIdentityRules.IsWholeChartBadge(b) ? 1m : profile.CoverageOf(b)))
                .ToArray();
            if (badges.Length > 0) result[group.Key] = badges;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> Handle(GetArchivedSkillTagsQuery request,
        CancellationToken cancellationToken)
    {
        return await _archive.GetArchivedTags(request.ChartIds, cancellationToken);
    }
}
