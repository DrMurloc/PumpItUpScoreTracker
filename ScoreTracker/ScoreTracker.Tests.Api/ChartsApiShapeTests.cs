using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Controllers.Api;

namespace ScoreTracker.Tests.Api;

public sealed class ChartsApiShapeTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly ChartsController _controller;

    public ChartsApiShapeTests()
    {
        _controller = new ChartsController(Mock.Of<ICurrentUserAccessor>(), _mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task GetRandomPreservesResponseShape()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1, ApiTestData.Chart2 });

        var result = await _controller.GetRandom(count: 2);

        JsonApproval.AssertWireShape("""
            [
              {
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
              },
              {
                "id": "22222222-2222-2222-2222-222222222222",
                "type": "Double",
                "shorthand": "D22",
                "level": 22,
                "noteCount": 845,
                "song": {
                  "name": "District 1",
                  "type": "Arcade",
                  "imagePath": "https://piuimages.example.com/district1.png"
                }
              }
            ]
            """, result);
    }

    [Fact]
    public async Task GetPreservesPagedResponseShape()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        var result = await _controller.Get("Phoenix", page: 1, count: 50);

        JsonApproval.AssertWireShape("""
            {
              "page": 1,
              "count": 1,
              "totalResults": 1,
              "results": [
                {
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
              ]
            }
            """, result);
    }

    // --- Mix parameter (additive, Phoenix 2). This endpoint's Mix predates the Phoenix 2 work:
    // --- it keeps accepting XX (legacy catalog reads) and its own options message, but omission
    // --- now defaults to Phoenix per the API-wide rule (previously omission was a 400).

    [Fact]
    public async Task GetOmittingMixDefaultsToPhoenixCatalog()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        var result = await _controller.Get(page: 1, count: 50);

        JsonApproval.AssertWireShape("""
            {
              "page": 1,
              "count": 1,
              "totalResults": 1,
              "results": [
                {
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
              ]
            }
            """, result);
        _mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Phoenix2", MixEnum.Phoenix2)]
    [InlineData("phoenix2", MixEnum.Phoenix2)]
    [InlineData("XX", MixEnum.XX)] // grandfathered: the catalog endpoint still serves the legacy mix
    public async Task GetThreadsRequestedMixIntoCatalogQuery(string raw, MixEnum expected)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        var result = await _controller.Get(raw, page: 1, count: 50);

        Assert.IsType<JsonResult>(result);
        _mediator.Verify(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == expected), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRejectsUnknownMixListingFullCatalogOptions()
    {
        var result = await _controller.Get("banana", page: 1, count: 50);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid Mix. Options are: XX,Phoenix,Phoenix2", (string)badRequest.Value!);
    }

    [Fact]
    public async Task GetRandomOmittingMixDefaultsToPhoenix()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        await _controller.GetRandom(count: 2);

        _mediator.Verify(
            m => m.Send(It.Is<GetRandomChartsQuery>(q => q.Mix == MixEnum.Phoenix),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRandomWithPhoenix2MixPreservesWireShapeAndThreadsMix()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });

        var result = await _controller.GetRandom(count: 1, mixString: "Phoenix2");

        JsonApproval.AssertWireShape("""
            [
              {
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
            ]
            """, result);
        _mediator.Verify(
            m => m.Send(It.Is<GetRandomChartsQuery>(q => q.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("banana")]
    public async Task GetRandomRejectsUnsupportedMixWithOptionsMessage(string mix)
    {
        var result = await _controller.GetRandom(count: 2, mixString: mix);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Mix is invalid, valid values: Phoenix, Phoenix2", (string)badRequest.Value!);
        _mediator.Verify(m => m.Send(It.IsAny<GetRandomChartsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
