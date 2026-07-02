using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
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
}
