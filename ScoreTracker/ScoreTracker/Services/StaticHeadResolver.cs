using MediatR;
using Microsoft.Extensions.Localization;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The head a route serves as static HTML. Title is the page's own text — App.razor
///     appends the brand to the document title, so a circuit's PageTitle can still take over
///     without the suffix flashing away. OgImage/Canonical are absent for routes the
///     resolver doesn't recognise; SongName/Artist feed the chart page's JSON-LD. For the
///     weekly hub Canonical is the clean path its filter/week variants fold into.
/// </summary>
public sealed record StaticHeadModel(string Title, string Description, string? OgImage, string? Canonical,
    string? SongName = null, string? Artist = null);

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

        // /Charts/{mix}/{song}/{difficulty} — the canonical chart page. Historical triples
        // 301 to canonical before rendering, so a rendered page is always self-canonical.
        if (!path.StartsWithSegments("/Charts", out var rest)) return null;
        var segments = rest.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not { Length: 3 }) return null;

        var resolution = await _charts.ResolveHistorical(segments[0], segments[1], segments[2],
            ChartUrlResolver.DefaultMix, cancellationToken);
        if (resolution == null) return null;
        var chart = await _charts.FindChart(resolution.ChartId, currentMix, cancellationToken);
        if (chart == null) return null;

        return new StaticHeadModel(
            $"{chart.Song.Name} {chart.DifficultyString}",
            await Description(chart, cancellationToken),
            chart.Song.ImagePath.ToString(),
            $"https://piuscores.arroweclip.se{resolution.CanonicalPath}",
            chart.Song.Name.ToString(),
            chart.Song.Artist.ToString());
    }

    /// <summary>
    ///     The search snippet. The population stats make each chart's description its own,
    ///     and substantial enough that engines quote it instead of stitching page text
    ///     together. The verdict caches daily per (chart, mix) — which also holds the
    ///     description stable between analytics rebuilds — and the page dispatches the same
    ///     query later in the same request, so this warms that cache rather than doubling
    ///     the work.
    /// </summary>
    private async Task<string> Description(Chart chart, CancellationToken cancellationToken)
    {
        var identity =
            $"Statistics and leaderboards for {chart.Song.Name} {chart.DifficultyString} by {chart.Song.Artist}.";
        const string tail = "Difficulty verdict, skill breakdown, and the full leaderboard on PIU Scores.";
        // The chart's own mix, not the viewer's: FindChart can fall back to another mix's
        // copy, and the population only exists where the chart does.
        var population = (await _mediator.Send(new GetChartVerdictQuery(chart.Id, chart.Mix), cancellationToken))
            .OfType<PopulationVerdict>().FirstOrDefault();
        if (population is not { ScoresTracked: > 0 }) return $"{identity} {tail}";

        var scores = population.ScoresTracked == 1 ? "score" : "scores";
        var passRate = (int)Math.Round(population.PassRate * 100);
        return $"{identity} {population.ScoresTracked:N0} {scores} tracked, {passRate}% pass rate. {tail}";
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
            "https://piuscores.arroweclip.se/WeeklyCharts");
    }
}
