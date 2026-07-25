using Microsoft.EntityFrameworkCore;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Catalog;

/// <summary>
///     Resolves every chart-specific title against a real catalog: for each title, does exactly one
///     chart in its mix satisfy <see cref="ISpecificChartTitle.AppliesToChart" />? A title whose song
///     name, chart type, or level misses the catalog matches nothing, so it never progresses and
///     never errors — the failure mode that froze seventeen titles (and with them SPECIALIST, which
///     needs all 90 skill titles) until 2026-07-24. A title matching several charts is the opposite
///     bug: it grants off an easier chart of the same song.
///     <para>
///         This is the check to run after editing a title's song/type/level, and after a mix's chart
///         levels shift. It needs a populated database, which CI does not have (its schema is
///         migrated and empty), so it lives here and skips unless configured:
///         <c>dotnet user-secrets set "CatalogProbe:ConnectionString" "..." --project ScoreTracker/ScoreTracker.AppHost</c>
///         then
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/... --filter "FullyQualifiedName~TitleChartResolutionProbe"</c>.
///         Read-only: it SELECTs the catalog and asserts.
///     </para>
///     <para>
///         The offline half of this guard — spelling drift between the two title lists, and the
///         corrected names pinned against regression — is
///         <c>ScoreTracker.Tests/DomainTests/TitleSongNameTests</c>, which does run in CI.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TitleChartResolutionProbeTests
{
    private readonly ITestOutputHelper _output;

    public TitleChartResolutionProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [CatalogProbeFact]
    public async Task Every_Phoenix2_chart_title_resolves_to_a_real_chart()
    {
        await AssertEveryChartTitleResolves(MixEnum.Phoenix2, Phoenix2TitleList.BuildList());
    }

    [CatalogProbeFact]
    public async Task Every_Phoenix_chart_title_resolves_to_a_real_chart()
    {
        await AssertEveryChartTitleResolves(MixEnum.Phoenix, PhoenixTitleList.BuildList());
    }

    private async Task AssertEveryChartTitleResolves(MixEnum mix, IEnumerable<PhoenixTitle> titles)
    {
        var charts = await LoadCatalog(mix, CancellationToken.None);
        Assert.True(charts.Count > 100,
            $"The configured database has only {charts.Count} {mix} charts — it is not a populated " +
            "catalog, so a resolution failure here would mean nothing. Point CatalogProbe:ConnectionString " +
            "at a prod-synced database.");

        var chartTitles = titles.OfType<ISpecificChartTitle>().ToArray();
        var resolutions = chartTitles
            .Select(t => (Title: t, Matches: charts.Where(t.AppliesToChart).ToArray()))
            .ToArray();

        var unresolved = resolutions.Where(r => r.Matches.Length == 0).ToArray();
        // Every title names exactly one chart — even the ones the official page renders with a "??"
        // stepball, which is a hidden level rather than a wildcard. So more than one match means
        // either a title that is too loose (it would grant off an easier chart of the same song) or
        // a duplicated catalog row; both are bugs.
        var ambiguous = resolutions.Where(r => r.Matches.Length > 1).ToArray();

        _output.WriteLine($"{mix}: {chartTitles.Length} chart titles against {charts.Count} charts — " +
                          $"{resolutions.Count(r => r.Matches.Length == 1)} resolve to exactly one, " +
                          $"{ambiguous.Length} to several, {unresolved.Length} to none.");

        Assert.True(unresolved.Length == 0,
            $"{unresolved.Length} {mix} title(s) name a chart the catalog does not have, so they can " +
            "never complete: " + string.Join(" · ", unresolved.Select(r =>
                $"{((PhoenixTitle)r.Title).Name} (\"{r.Title.SongName}\" — {((PhoenixTitle)r.Title).Description})")));
        Assert.True(ambiguous.Length == 0,
            $"{ambiguous.Length} {mix} title(s) match more than one chart, so an easier chart of the " +
            "same song grants them: " + string.Join(" · ", ambiguous.Select(r =>
                $"{((PhoenixTitle)r.Title).Name} -> {string.Join(", ", r.Matches.Select(c => c.DifficultyString))}")));
    }

    /// <summary>
    ///     The mix's charts as the domain sees them. Only the fields
    ///     <see cref="ISpecificChartTitle.AppliesToChart" /> reads — song name, type, level — come from
    ///     the database; the rest of <see cref="Chart" /> is filled with placeholders, because a probe
    ///     that hydrated jackets and BPMs would fail on unrelated catalog gaps.
    /// </summary>
    private static async Task<IReadOnlyList<Chart>> LoadCatalog(MixEnum mix, CancellationToken cancellationToken)
    {
        await using var database = CreateContext();
        var mixId = await database.Mix.Where(m => m.Name == mix.ToString())
            .Select(m => m.Id).SingleAsync(cancellationToken);

        var rows = await (from chartMix in database.ChartMix
                where chartMix.MixId == mixId
                join chart in database.Chart on chartMix.ChartId equals chart.Id
                join song in database.Song on chart.SongId equals song.Id
                select new { chart.Id, SongName = song.Name, chart.Type, chartMix.Level })
            .ToArrayAsync(cancellationToken);

        var placeholderImage = new Uri("https://piuscores.arroweclip.se/probe.png");
        return rows
            .Where(r => Enum.TryParse<ChartType>(r.Type, out _) && r.Level >= DifficultyLevel.Min &&
                        r.Level <= DifficultyLevel.Max && !string.IsNullOrWhiteSpace(r.SongName))
            .Select(r => new Chart(r.Id, mix,
                new Song(Name.From(r.SongName), SongType.Arcade, placeholderImage, TimeSpan.Zero,
                    Name.From("Probe"), null),
                Enum.Parse<ChartType>(r.Type), r.Level, mix, null, null, new HashSet<Skill>()))
            .ToArray();
    }

    private static ChartAttemptDbContext CreateContext()
    {
        return new ChartAttemptDbContext(new DbContextOptionsBuilder<ChartAttemptDbContext>()
                .UseSqlServer(CatalogProbeConfiguration.ConnectionString)
                .Options,
            VerticalModelContributions.All());
    }
}
