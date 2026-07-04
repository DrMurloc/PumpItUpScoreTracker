using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
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

    // --- Mix parameter (additive, Phoenix 2): each mix runs its own weekly board; the API
    // --- default is Phoenix permanently, never the caller's current on-site mix.

    private void SetupBoard()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(ApiTestData.ChartId1, ApiTestData.Date2) });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
    }

    private void SetupEntries()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentEntry(ApiTestData.PublicUserId, ApiTestData.ChartId1,
                    PhoenixScore.From(985000), PhoenixPlate.ExtremeGame, false, null, 0)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.PublicUser });
    }

    [Fact]
    public async Task GetWeeklyChartsOmittingMixReadsPhoenixBoard()
    {
        SetupBoard();

        await _controller.GetWeeklyCharts();

        _mediator.Verify(
            m => m.Send(It.Is<GetWeeklyChartsQuery>(q => q.Mix == MixEnum.Phoenix),
                It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetWeeklyChartsWithPhoenix2MixPreservesWireShapeAndReadsThatBoard()
    {
        SetupBoard();

        var result = await _controller.GetWeeklyCharts(mixString: "Phoenix2");

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
            m => m.Send(It.Is<GetWeeklyChartsQuery>(q => q.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("banana")]
    public async Task GetWeeklyChartsRejectsUnsupportedMixWithOptionsMessage(string mix)
    {
        var result = await _controller.GetWeeklyCharts(mixString: mix);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Mix is invalid, valid values: Phoenix, Phoenix2", (string)badRequest.Value!);
        _mediator.Verify(m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetWeeklyChartScoresOmittingMixReadsPhoenixEntries()
    {
        SetupEntries();

        await _controller.GetWeeklyChartScores(null);

        _mediator.Verify(
            m => m.Send(It.Is<GetWeeklyChartEntriesQuery>(q => q.Mix == MixEnum.Phoenix),
                It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetWeeklyChartScoresWithPhoenix2MixPreservesWireShapeAndReadsThoseEntries()
    {
        SetupEntries();

        var result = await _controller.GetWeeklyChartScores(null, "Phoenix2");

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
              }
            ]
            """, result);
        _mediator.Verify(
            m => m.Send(It.Is<GetWeeklyChartEntriesQuery>(q => q.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("banana")]
    public async Task GetWeeklyChartScoresRejectsUnsupportedMixWithOptionsMessage(string mix)
    {
        var result = await _controller.GetWeeklyChartScores(null, mix);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Mix is invalid, valid values: Phoenix, Phoenix2", (string)badRequest.Value!);
        _mediator.Verify(m => m.Send(It.IsAny<GetWeeklyChartEntriesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
