using MediatR;
using Microsoft.Extensions.Localization;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The head a route serves as static HTML. OgImage is absent when the route has no art.
///     Canonical is the clean path query variants of the route fold into; absent when the
///     route has no variants worth folding.
/// </summary>
public sealed record StaticHeadModel(string Title, string Description, string? OgImage,
    string? Canonical = null);

/// <summary>
///     Resolves the document head from the request path
///     (docs/design/seo-friendly-site.md §4). Crawlers, unfurlers and LLM readers see only
///     this head — PageTitle and HeadContent render inside a circuit they never run. In a
///     browser the circuit's PageTitle replaces the static title after boot, so titles here
///     match the page's own text and the swap never shows. Static-SSR pages have no circuit
///     at all, so this head is their whole head. Null means an unmatched route: App.razor
///     falls back to the bare site title with no description, because one shared description
///     on every URL reads as sitewide duplicate content.
/// </summary>
public sealed class StaticHeadResolver
{
    private readonly ChartUrlResolver _charts;
    private readonly IStringLocalizer<App> _localizer;
    private readonly IMediator _mediator;

    public StaticHeadResolver(ChartUrlResolver charts, IMediator mediator,
        IStringLocalizer<App> localizer)
    {
        _charts = charts;
        _mediator = mediator;
        _localizer = localizer;
    }

    public async Task<StaticHeadModel?> Resolve(PathString path, MixEnum currentMix,
        CancellationToken cancellationToken)
    {
        if (path.Equals("/WeeklyCharts", StringComparison.OrdinalIgnoreCase))
            return await ResolveWeeklyCharts(currentMix, cancellationToken);

        if (!path.StartsWithSegments("/Chart", out var rest)) return null;
        if (!Guid.TryParse(rest.Value?.Trim('/'), out var chartId)) return null;

        var chart = await _charts.FindChart(chartId, currentMix, cancellationToken);
        if (chart == null) return null;

        // The title is the chart page's own PageTitle text; the description is its
        // (previously circuit-only) meta description, now served where a crawler reads it.
        return new StaticHeadModel(
            $"{chart.Song.Name} {chart.DifficultyString}",
            $"Statistics and leaderboards for {chart.Song.Name} {chart.DifficultyString} by {chart.Song.Artist}.",
            chart.Song.ImagePath.ToString());
    }

    /// <summary>
    ///     The challenges hub's head (weekly-charts-overhaul.md §3.4): the concept copy the
    ///     fold no longer holds rides the description, the daily jacket (or the week's first)
    ///     is the unfurl art, and every filter/week variant folds into the clean URL. Mixes
    ///     without weekly boards read Phoenix, mirroring the page.
    /// </summary>
    private async Task<StaticHeadModel> ResolveWeeklyCharts(MixEnum currentMix,
        CancellationToken cancellationToken)
    {
        var mix = currentMix is MixEnum.Phoenix or MixEnum.Phoenix2 ? currentMix : MixEnum.Phoenix;
        var board = await _mediator.Send(new GetWeeklyBoardQuery(mix), cancellationToken);
        var daily = await _mediator.Send(new GetDailyStepBoardQuery(mix), cancellationToken);

        var jacketChartId = daily?.Board.ChartId
                            ?? board.Charts.Select(c => (Guid?)c.ChartId).FirstOrDefault();
        var jacket = jacketChartId is { } id
            ? (await _charts.FindChart(id, mix, cancellationToken))?.Song.ImagePath.ToString()
            : null;

        return new StaticHeadModel(
            _localizer["Weekly Charts"],
            _localizer[
                "{0} Pump It Up challenge charts this week, a daily chart with live standings, and the monthly PUMBILITY board.",
                board.Charts.Count],
            jacket,
            "/WeeklyCharts");
    }
}
