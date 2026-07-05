using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api;

namespace ScoreTracker.Tests.Api;

public sealed class TierListsApiShapeTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly TierListsController _controller;

    public TierListsApiShapeTests()
    {
        _controller = new TierListsController(_mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    // The controller dispatches the fallback-aware tier list query so it can serve the RAW
    // per-mix list (discarding provisional Phoenix-1 stand-ins) — the API never silently swaps
    // Phoenix data into a Phoenix2 response.
    private void SetupTierList(TierListResult result)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    [Fact]
    public async Task PassCountTierListPreservesResponseShapeAndFiltersUnknownCharts()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        SetupTierList(new TierListResult(new[]
        {
            new SongTierListEntry(Name.From("Pass Count"), ApiTestData.ChartId1, TierListCategory.Easy, 1),
            // chart not in the requested type/level folder — the endpoint filters it out
            new SongTierListEntry(Name.From("Pass Count"), ApiTestData.ChartId2, TierListCategory.Hard, 2)
        }, false));

        var result = await _controller.GetPassCountTierList("Single", 20);

        JsonApproval.AssertWireShape("""
            [
              {
                "category": "Easy",
                "order": 1,
                "chart": {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "type": "Single",
                  "shorthand": "S20",
                  "level": 20,
                  "noteCount": 731,
                  "song": {
                    "name": "Conflict",
                    "type": "Arcade",
                    "imagePath": "https://piuimages.example.com/conflict.png"
                  }
                }
              }
            ]
            """, result);
    }

    // --- Mix parameter (additive, Phoenix 2): default Phoenix permanently; Phoenix/Phoenix2 only.

    private async Task<IActionResult> CallEndpoint(string endpoint, string? mixString)
    {
        return endpoint switch
        {
            "officialscores" => await _controller.GetOfficialScoresTierList("Single", 20, mixString),
            "passcount" => await _controller.GetPassCountTierList("Single", 20, mixString),
            "popularity" => await _controller.GetPopularityTierList("Single", 20, mixString),
            _ => await _controller.GetScoresTierList("Single", 20, mixString)
        };
    }

    [Theory]
    [InlineData("officialscores", "Official Scores")]
    [InlineData("passcount", "Pass Count")]
    [InlineData("popularity", "Popularity")]
    [InlineData("scores", "Scores")]
    public async Task TierListEndpointsOmittingMixRequestPhoenix(string endpoint, string tierListName)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        SetupTierList(new TierListResult(Array.Empty<SongTierListEntry>(), false));

        await CallEndpoint(endpoint, null);

        _mediator.Verify(
            m => m.Send(
                It.Is<GetTierListWithFallbackQuery>(q =>
                    q.Mix == MixEnum.Phoenix && q.TierListName.ToString() == tierListName),
                It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("officialscores", "Official Scores")]
    [InlineData("passcount", "Pass Count")]
    [InlineData("popularity", "Popularity")]
    [InlineData("scores", "Scores")]
    public async Task TierListEndpointsThreadPhoenix2MixIntoQueries(string endpoint, string tierListName)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        SetupTierList(new TierListResult(Array.Empty<SongTierListEntry>(), false));

        await CallEndpoint(endpoint, "Phoenix2");

        _mediator.Verify(
            m => m.Send(
                It.Is<GetTierListWithFallbackQuery>(q =>
                    q.Mix == MixEnum.Phoenix2 && q.TierListName.ToString() == tierListName),
                It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PassCountTierListWithPhoenix2MixPreservesWireShape()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        SetupTierList(new TierListResult(new[]
        {
            new SongTierListEntry(Name.From("Pass Count"), ApiTestData.ChartId1, TierListCategory.Easy, 1)
        }, false));

        var result = await _controller.GetPassCountTierList("Single", 20, "Phoenix2");

        JsonApproval.AssertWireShape("""
            [
              {
                "category": "Easy",
                "order": 1,
                "chart": {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "type": "Single",
                  "shorthand": "S20",
                  "level": 20,
                  "noteCount": 731,
                  "song": {
                    "name": "Conflict",
                    "type": "Arcade",
                    "imagePath": "https://piuimages.example.com/conflict.png"
                  }
                }
              }
            ]
            """, result);
    }

    [Fact]
    public async Task Phoenix2ProvisionalFallbackIsServedAsEmptyRawList()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        // The site UI badges this Phoenix-1 stand-in; the API must serve the raw (still empty)
        // Phoenix 2 list instead, so integrations don't see content flip once P2 votes exist.
        SetupTierList(new TierListResult(new[]
        {
            new SongTierListEntry(Name.From("Pass Count"), ApiTestData.ChartId1, TierListCategory.Easy, 1)
        }, true));

        var result = await _controller.GetPassCountTierList("Single", 20, "Phoenix2");

        JsonApproval.AssertWireShape("[]", result);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("banana")]
    public async Task TierListEndpointsRejectUnsupportedMixWithOptionsMessage(string mix)
    {
        foreach (var endpoint in new[] { "officialscores", "passcount", "popularity", "scores" })
        {
            var result = await CallEndpoint(endpoint, mix);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Mix is invalid, valid values: Phoenix, Phoenix2", (string)badRequest.Value!);
        }

        _mediator.Verify(
            m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
