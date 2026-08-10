using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The /Charts/Export.csv UI-support endpoint. Deliberately OUTSIDE the api/* wire
///     contract — these are behavior facts (hygiene, auth gating, shapes), not goldens.
/// </summary>
public sealed class ChartsExportControllerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();
    private SearchChartsQuery? _lastQuery;

    public ChartsExportControllerTests()
    {
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MixEnum.Phoenix);
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        _mediator.Setup(m => m.Send(It.IsAny<SearchChartsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ChartSearchResultPage>, CancellationToken>((q, _) => _lastQuery = (SearchChartsQuery)q)
            .ReturnsAsync(new ChartSearchResultPage(new[] { MakeResult() }, 1));
    }

    private static ChartSearchResult MakeResult()
    {
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song("=SUM(A1), \"Danger\"", SongType.Arcade, new Uri("https://piu.test/a.png"),
                TimeSpan.FromSeconds(125), "BanYa", Bpm.From(160, 160)),
            ChartType.Double, 19, MixEnum.Phoenix, null, 700, new HashSet<Skill>());
        return new ChartSearchResult(chart, MixEnum.XX,
            Array.Empty<ChartBadge>(), 10.5m, TierListCategory.Hard, null, null, 19.6, null, 40, 1,
            new ChartSearchMyState(950000, PhoenixLetterGrade.AAA, null, null, null, false,
                true, DateTimeOffset.Parse("2026-06-01T00:00:00Z")));
    }

    private ChartsExportController BuildController(string queryString)
    {
        var controller = new ChartsExportController(_mediator.Object, _currentUser.Object, _uiSettings.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.QueryString = new QueryString(queryString);
        return controller;
    }

    private static string Content(IActionResult result)
    {
        return Encoding.UTF8.GetString(Assert.IsType<FileContentResult>(result).FileContents);
    }

    [Fact]
    public async Task TheFilteredSetExportsUnpagedWithStableHeaders()
    {
        var result = await BuildController("?LevelMin=19&Columns=Song,Level,Mix").Export(CancellationToken.None);

        var csv = Content(result);
        Assert.StartsWith("Song,Level,Mix", csv);
        Assert.Null(_lastQuery!.Page);
        Assert.Equal(19, _lastQuery.LevelMin);
    }

    [Fact]
    public async Task FormulaInjectionAndQuotingAreEscaped()
    {
        var result = await BuildController("?Columns=Song").Export(CancellationToken.None);

        var lines = Content(result).TrimEnd().Split('\n');
        // The =-led name gets the apostrophe guard, and the whole field quotes because of
        // its comma and embedded quotes.
        Assert.Equal("\"'=SUM(A1), \"\"Danger\"\"\"", lines[1].TrimEnd());
    }

    [Fact]
    public async Task AnonymousCallersHaveMyColumnsSilentlyDropped()
    {
        var result = await BuildController("?Columns=Song,MyPhoenixScore").Export(CancellationToken.None);

        Assert.Equal("Song", Content(result).Split('\n')[0].TrimEnd());
    }

    [Fact]
    public async Task SignedInCallersExportTheirLinkedMixState()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(new User(
            Guid.NewGuid(), "Tester", true, null, new Uri("https://piu.test/a.png"), null));

        var result = await BuildController("?Columns=MyPhoenixScore").Export(CancellationToken.None);

        var lines = Content(result).TrimEnd().Split('\n').Select(l => l.TrimEnd()).ToArray();
        Assert.Equal("MyPhoenixScore", lines[0]);
        Assert.Equal("950000", lines[1]);
        Assert.Equal(_lastQuery!.UserId, _currentUser.Object.User.Id);
    }

    [Fact]
    public async Task ColumnsWriteInRegistryOrderRegardlessOfRequestOrder()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(new User(
            Guid.NewGuid(), "Tester", true, null, new Uri("https://piu.test/a.png"), null));

        var result = await BuildController("?Columns=Mix,Level,MyPhoenixScore")
            .Export(CancellationToken.None);

        var lines = Content(result).TrimEnd().Split('\n').Select(l => l.TrimEnd()).ToArray();
        Assert.Equal(2, lines.Length);
        // Registry order, not the order the caller asked in — stable files.
        Assert.Equal("Level,Mix,MyPhoenixScore", lines[0]);
        Assert.Equal("19,Phoenix,950000", lines[1]);
    }

    /// <summary>
    ///     The fixture is a D19 AAA on a 125-second Arcade chart. Phoenix prices that at
    ///     LevelRatings[19] x the AAA modifier, with no time adjustment — the number the
    ///     /Pumbility pool would give the same record.
    /// </summary>
    [Fact]
    public async Task PumbilityPricesTheRecordWithTheMixFormula()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(new User(
            Guid.NewGuid(), "Tester", true, null, new Uri("https://piu.test/a.png"), null));

        var result = await BuildController("?Columns=Pumbility").Export(CancellationToken.None);

        var lines = Content(result).TrimEnd().Split('\n').Select(l => l.TrimEnd()).ToArray();
        Assert.Equal("Pumbility", lines[0]);
        var expected = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, true)
            .GetScore(MakeResult().Chart, 950000, PhoenixPlate.RoughGame, false);
        Assert.Equal(expected.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), lines[1]);
    }

    [Fact]
    public async Task PumbilityIsNotOfferedOnALegacyMixWhereItHasNoFormula()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(new User(
            Guid.NewGuid(), "Tester", true, null, new Uri("https://piu.test/a.png"), null));

        // PumbilityScoring throws outside the Phoenix family, so the scope has to drop the
        // column rather than the endpoint 500 on a saved setting naming it.
        var result = await BuildController("?Mix=XX&Columns=Song,Pumbility").Export(CancellationToken.None);

        Assert.Equal("Song", Content(result).Split('\n')[0].TrimEnd());
    }
}
