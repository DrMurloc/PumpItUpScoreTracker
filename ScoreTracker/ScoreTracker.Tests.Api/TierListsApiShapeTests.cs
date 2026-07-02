using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;
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

    [Fact]
    public async Task PassCountTierListPreservesResponseShapeAndFiltersUnknownCharts()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SongTierListEntry(Name.From("Pass Count"), ApiTestData.ChartId1, TierListCategory.Easy, 1),
                // chart not in the requested type/level folder — the endpoint filters it out
                new SongTierListEntry(Name.From("Pass Count"), ApiTestData.ChartId2, TierListCategory.Hard, 2)
            });

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
}
