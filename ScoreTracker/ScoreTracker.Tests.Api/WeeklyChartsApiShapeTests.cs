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
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.Web.Controllers.Api;

namespace ScoreTracker.Tests.Api;

public sealed class WeeklyChartsApiShapeTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly WeeklyChartsController _controller;

    public WeeklyChartsApiShapeTests()
    {
        _controller = new WeeklyChartsController(_mediator.Object, _users.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task GetWeeklyChartsPreservesResponseShape()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentChart(ApiTestData.ChartId1, ApiTestData.Date2),
                new WeeklyTournamentChart(ApiTestData.ChartId2, ApiTestData.Date2)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1, ApiTestData.Chart2 });

        var result = await _controller.GetWeeklyCharts();

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
    public async Task GetWeeklyChartScoresPreservesResponseShapeAndAnonymizesPrivatePlayers()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentEntry(ApiTestData.PublicUserId, ApiTestData.ChartId1,
                    PhoenixScore.From(985000), PhoenixPlate.ExtremeGame, false, null, 0),
                new WeeklyTournamentEntry(ApiTestData.PrivateUserId, ApiTestData.ChartId2,
                    PhoenixScore.From(999000), PhoenixPlate.PerfectGame, false, null, 0)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1, ApiTestData.Chart2 });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.PublicUser, ApiTestData.PrivateUser });

        var result = await _controller.GetWeeklyChartScores(null);

        JsonApproval.AssertWireShape("""
            [
              {
                "chartId": "11111111-1111-1111-1111-111111111111",
                "player": {
                  "username": "VisiblePlayer",
                  "gameTag": "VISIBL",
                  "country": "Canada",
                  "avatarUrl": "https://piuimages.example.com/avatar1.png"
                },
                "score": {
                  "score": 985000,
                  "plate": "ExtremeGame",
                  "letterGrade": "SS\u002B",
                  "isBroken": false
                }
              },
              {
                "chartId": "22222222-2222-2222-2222-222222222222",
                "player": {
                  "username": "Anonymous",
                  "gameTag": "",
                  "country": "",
                  "avatarUrl": "https://piuimages.example.com/avatar2.png"
                },
                "score": {
                  "score": 999000,
                  "plate": "PerfectGame",
                  "letterGrade": "SSS\u002B",
                  "isBroken": false
                }
              }
            ]
            """, result);
    }
}
