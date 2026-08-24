using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web;
using ScoreTracker.Web.Services;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The head crawlers, unfurlers and LLM readers actually see. The rerate clause matters
///     across thousands of already-indexed chart URLs, so it is pinned here rather than left
///     to a visual check.
/// </summary>
public sealed class StaticHeadResolverTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _chartId = Guid.NewGuid();

    private StaticHeadResolver Build()
    {
        var urls = new ChartUrlResolver(_mediator.Object, new MemoryCache(new MemoryCacheOptions()));
        return new StaticHeadResolver(urls, _mediator.Object, PassThroughLocalizer());
    }

    // Keys are English UI text verbatim, so the key IS the display string.
    private static IStringLocalizer<App> PassThroughLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        return localizer.Object;
    }

    [Theory]
    [InlineData("/PumbilityCalculator/phoenix-2", MixEnum.Phoenix, "Phoenix 2", "phoenix-2")]
    [InlineData("/PumbilityCalculator/phoenix", MixEnum.Phoenix2, "Phoenix", "phoenix")]
    [InlineData("/PumbilityCalculator", MixEnum.Phoenix2, "Phoenix 2", "phoenix-2")]
    [InlineData("/PumbilityCalculator", MixEnum.Phoenix, "Phoenix", "phoenix")]
    [InlineData("/PumbilityCalculator", MixEnum.XX, "Phoenix 2", "phoenix-2")]
    public async Task ThePumbilityCalculatorHeadIsOnePerMixAndSelfCanonical(string path, MixEnum viewerMix,
        string mixName, string canonicalSlug)
    {
        // The slug wins; the bare route serves the viewer's mix, or the newest mix with a formula
        // when the viewer's has none. No query behind it — the formula is a constant.
        var head = await Build().Resolve(path, viewerMix, CancellationToken.None);

        Assert.NotNull(head);
        Assert.Equal($"PUMBILITY Calculator — {mixName}", head!.Title);
        Assert.Equal($"https://piuscores.arroweclip.se/PumbilityCalculator/{canonicalSlug}", head.Canonical);
        Assert.Contains("PUMBILITY formula", head.Description);
        Assert.Contains(mixName == "Phoenix 2" ? "Base(level) × (grade + plate)" : "Base(level) × grade", head.Description);
        Assert.NotNull(head.Calculator);
        Assert.Equal(mixName, head.Calculator!.MixName);
        Assert.Null(head.OgImage);
        Assert.Null(head.SongName);
        Assert.Null(head.MixDiff);
    }

    [Theory]
    [InlineData("/PhoenixCalculator/phoenix-2", MixEnum.Phoenix, "Phoenix 2", "phoenix-2")]
    [InlineData("/PhoenixCalculator/phoenix", MixEnum.Phoenix2, "Phoenix", "phoenix")]
    [InlineData("/PhoenixCalculator", MixEnum.Phoenix2, "Phoenix 2", "phoenix-2")]
    [InlineData("/PhoenixCalculator", MixEnum.Phoenix, "Phoenix", "phoenix")]
    [InlineData("/PhoenixCalculator", MixEnum.XX, "Phoenix 2", "phoenix-2")]
    public async Task ThePhoenixCalculatorHeadIsOnePerMixAndSelfCanonical(string path, MixEnum viewerMix,
        string mixName, string canonicalSlug)
    {
        // Same rules as the PUMBILITY page: the slug wins, the bare route — the pre-rebuild URL —
        // serves the viewer's mix, and every rendered page is self-canonical.
        var head = await Build().Resolve(path, viewerMix, CancellationToken.None);

        Assert.NotNull(head);
        Assert.Equal($"Phoenix Score Calculator — {mixName}", head!.Title);
        Assert.Equal($"https://piuscores.arroweclip.se/PhoenixCalculator/{canonicalSlug}", head.Canonical);
        Assert.Contains("score formula", head.Description);
        Assert.Contains(mixName == "Phoenix 2" ? "AA 920k" : "AA 900k", head.Description);
        Assert.NotNull(head.ScoreCalculator);
        Assert.Equal(mixName, head.ScoreCalculator!.MixName);
        Assert.Null(head.Calculator);
    }

    [Theory]
    [InlineData("/PhoenixCalculator/xx")]
    [InlineData("/PhoenixCalculator/nonsense")]
    [InlineData("/PhoenixCalculator/phoenix-2/extra")]
    public async Task AMixWithoutPhoenixScoringOrAnUnknownSlugHasNoScoreCalculatorHead(string path)
    {
        Assert.Null(await Build().Resolve(path, MixEnum.Phoenix2, CancellationToken.None));
    }

    [Theory]
    [InlineData("/PumbilityCalculator/xx")]
    [InlineData("/PumbilityCalculator/nonsense")]
    [InlineData("/PumbilityCalculator/phoenix-2/doubles")]
    public async Task AMixWithoutAFormulaOrAnUnknownSlugHasNoCalculatorHead(string path)
    {
        Assert.Null(await Build().Resolve(path, MixEnum.Phoenix2, CancellationToken.None));
    }

    private void Catalog(MixEnum mix, params Chart[] charts)
    {
        _mediator.Setup(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    private void EmptyCatalogs()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
    }

    private void Verdicts(params ChartVerdictFacet[] facets)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartVerdictQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(facets);
    }

    private static Chart Chart(Guid id, MixEnum mix, int level)
    {
        return ChartSlugsTests.BuildChart(id, "Iolite Sky", mix, level, ChartType.Double);
    }

    private Task<StaticHeadModel?> Resolve(MixEnum mix, string path)
    {
        return Build().Resolve(new PathString(path), mix, CancellationToken.None);
    }

    [Fact]
    public async Task ARerateIsNamedInTheDescriptionWithTheMixItMovedFrom()
    {
        EmptyCatalogs();
        Catalog(MixEnum.Phoenix2, Chart(_chartId, MixEnum.Phoenix2, 21));
        Verdicts(new HistoryVerdict(MixEnum.XX, new[]
        {
            new MixLevelRecord(MixEnum.XX, 20),
            new MixLevelRecord(MixEnum.Phoenix, 20),
            new MixLevelRecord(MixEnum.Phoenix2, 21)
        }));

        var head = await Resolve(MixEnum.Phoenix2, "/Charts/phoenix-2/iolite-sky/d21");

        Assert.NotNull(head);
        Assert.Contains("Rerated from D20 in Phoenix.", head!.Description);
    }

    [Fact]
    public async Task TheRerateComparesAgainstThePreviousMixNotTheDebut()
    {
        // A chart that moved twice must not report its debut level — the page is answering
        // "where did it go", and the answer is where it came from last.
        EmptyCatalogs();
        Catalog(MixEnum.Phoenix2, Chart(_chartId, MixEnum.Phoenix2, 22));
        Verdicts(new HistoryVerdict(MixEnum.XX, new[]
        {
            new MixLevelRecord(MixEnum.XX, 19),
            new MixLevelRecord(MixEnum.Phoenix, 21),
            new MixLevelRecord(MixEnum.Phoenix2, 22)
        }));

        var head = await Resolve(MixEnum.Phoenix2, "/Charts/phoenix-2/iolite-sky/d22");

        Assert.Contains("Rerated from D21 in Phoenix.", head!.Description);
        Assert.DoesNotContain("D19", head.Description);
    }

    [Fact]
    public async Task AChartThatHeldItsLevelSaysNothingAboutRerates()
    {
        EmptyCatalogs();
        Catalog(MixEnum.Phoenix2, Chart(_chartId, MixEnum.Phoenix2, 20));
        Verdicts(new HistoryVerdict(MixEnum.XX, new[]
        {
            new MixLevelRecord(MixEnum.XX, 20),
            new MixLevelRecord(MixEnum.Phoenix, 20),
            new MixLevelRecord(MixEnum.Phoenix2, 20)
        }));

        var head = await Resolve(MixEnum.Phoenix2, "/Charts/phoenix-2/iolite-sky/d20");

        Assert.DoesNotContain("Rerated", head!.Description);
    }

    [Fact]
    public async Task ADebutingChartHasNoPreviousMixToCompareAgainst()
    {
        EmptyCatalogs();
        Catalog(MixEnum.Phoenix2, Chart(_chartId, MixEnum.Phoenix2, 21));
        Verdicts(new HistoryVerdict(MixEnum.Phoenix2, new[]
        {
            new MixLevelRecord(MixEnum.Phoenix2, 21)
        }));

        var head = await Resolve(MixEnum.Phoenix2, "/Charts/phoenix-2/iolite-sky/d21");

        Assert.DoesNotContain("Rerated", head!.Description);
    }

    [Fact]
    public async Task AChartWithNoHistoryFacetStillGetsItsOrdinaryDescription()
    {
        EmptyCatalogs();
        Catalog(MixEnum.Phoenix2, Chart(_chartId, MixEnum.Phoenix2, 21));
        Verdicts();

        var head = await Resolve(MixEnum.Phoenix2, "/Charts/phoenix-2/iolite-sky/d21");

        Assert.NotNull(head);
        Assert.Contains("Iolite Sky D21", head!.Description);
        Assert.DoesNotContain("Rerated", head.Description);
    }

    [Fact]
    public async Task TheRerateClauseSitsBeforeThePopulationStatsSoASnippetKeepsIt()
    {
        EmptyCatalogs();
        Catalog(MixEnum.Phoenix2, Chart(_chartId, MixEnum.Phoenix2, 21));
        Verdicts(
            new HistoryVerdict(MixEnum.Phoenix, new[]
            {
                new MixLevelRecord(MixEnum.Phoenix, 20),
                new MixLevelRecord(MixEnum.Phoenix2, 21)
            }),
            new PopulationVerdict(1204, 0.71));

        var head = await Resolve(MixEnum.Phoenix2, "/Charts/phoenix-2/iolite-sky/d21");

        var description = head!.Description;
        Assert.Contains("Rerated from D20 in Phoenix.", description);
        Assert.Contains("1,204 scores tracked, 71% pass rate.", description);
        Assert.True(description.IndexOf("Rerated", StringComparison.Ordinal)
                    < description.IndexOf("1,204", StringComparison.Ordinal));
    }
}
