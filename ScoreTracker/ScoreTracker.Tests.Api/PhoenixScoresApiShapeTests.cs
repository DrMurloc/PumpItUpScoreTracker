using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Controllers.Api;

namespace ScoreTracker.Tests.Api;

public sealed class PhoenixScoresApiShapeTests
{
    [Fact]
    public async Task GetPreservesPagedResponseShapeIncludingBrokenScorelessRecords()
    {
        var mediator = new Mock<IMediator>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(c => c.User).Returns(ApiTestData.PublicUser);
        mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(ApiTestData.ChartId1, PhoenixScore.From(985000),
                    PhoenixPlate.ExtremeGame, false, ApiTestData.Date2),
                new RecordedPhoenixScore(ApiTestData.ChartId2, null, null, true, ApiTestData.Date1)
            });
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1, ApiTestData.Chart2 });
        var controller = new PhoenixScoresController(currentUser.Object, mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Get(page: 1, count: 50);

        JsonApproval.AssertWireShape("""
            {
              "page": 1,
              "count": 2,
              "totalResults": 2,
              "results": [
                {
                  "plate": "Extreme Game",
                  "letterGrade": "SS\u002B",
                  "score": 985000,
                  "isBroken": false,
                  "recordedDate": "2026-02-20T00:00:00+00:00",
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
                },
                {
                  "plate": null,
                  "letterGrade": null,
                  "score": null,
                  "isBroken": true,
                  "recordedDate": "2026-01-15T00:00:00+00:00",
                  "chart": {
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
                }
              ]
            }
            """, result);
    }
}
