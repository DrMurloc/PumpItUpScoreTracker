using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api;
using ScoreTracker.Web.Dtos.Api;

namespace ScoreTracker.Tests.Api;

public sealed class PhoenixScoresApiShapeTests
{
    private static (PhoenixScoresController Controller, Mock<IMediator> Mediator) BuildVerifiableController(
        params RecordedPhoenixScore[] records)
    {
        var mediator = new Mock<IMediator>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(c => c.User).Returns(ApiTestData.PublicUser);
        mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1, ApiTestData.Chart2 });
        var controller = new PhoenixScoresController(currentUser.Object, mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return (controller, mediator);
    }

    private static PhoenixScoresController BuildController(params RecordedPhoenixScore[] records)
    {
        return BuildVerifiableController(records).Controller;
    }

    private static RecordedPhoenixScore ScoredS20 { get; } = new(ApiTestData.ChartId1, PhoenixScore.From(985000),
        PhoenixPlate.ExtremeGame, false, ApiTestData.Date2);

    private static RecordedPhoenixScore BrokenScoreless { get; } =
        new(ApiTestData.ChartId2, null, null, true, ApiTestData.Date1);

    private static RecordedPhoenixScore ScoredD22 { get; } = new(ApiTestData.ChartId2, PhoenixScore.From(995000),
        PhoenixPlate.SuperbGame, false, ApiTestData.Date1);

    [Fact]
    public async Task GetPreservesPagedResponseShapeIncludingBrokenScorelessRecords()
    {
        var controller = BuildController(ScoredS20, BrokenScoreless);

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
                  "pumbility": 897,
                  "pumbilityPlus": 897,
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
                  "pumbility": null,
                  "pumbilityPlus": null,
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

    [Fact]
    public async Task GetSortsByPumbilityDescendingWithScorelessRecordsLast()
    {
        var controller = BuildController(ScoredS20, BrokenScoreless, ScoredD22);

        var result = await controller.Get(page: 1, count: 50, sortBy: "Pumbility");

        JsonApproval.AssertWireShape("""
            {
              "page": 1,
              "count": 3,
              "totalResults": 3,
              "results": [
                {
                  "plate": "Superb Game",
                  "letterGrade": "SSS\u002B",
                  "score": 995000,
                  "pumbility": 1320,
                  "pumbilityPlus": 1320,
                  "isBroken": false,
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
                },
                {
                  "plate": "Extreme Game",
                  "letterGrade": "SS\u002B",
                  "score": 985000,
                  "pumbility": 897,
                  "pumbilityPlus": 897,
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
                  "pumbility": null,
                  "pumbilityPlus": null,
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

    [Fact]
    public async Task GetAppliesLevelAndPlateFiltersBeforePaging()
    {
        var controller = BuildController(ScoredS20, BrokenScoreless, ScoredD22);

        var result = await controller.Get(page: 1, count: 50, minLevel: 21, minPlate: "Superb Game");

        JsonApproval.AssertWireShape("""
            {
              "page": 1,
              "count": 1,
              "totalResults": 1,
              "results": [
                {
                  "plate": "Superb Game",
                  "letterGrade": "SSS\u002B",
                  "score": 995000,
                  "pumbility": 1320,
                  "pumbilityPlus": 1320,
                  "isBroken": false,
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

    [Theory]
    [InlineData("SortBy", "banana")]
    [InlineData("SortDir", "sideways")]
    [InlineData("ChartType", "Quad")]
    [InlineData("MinLetterGrade", "Z")]
    [InlineData("MinPlate", "Wooden Game")]
    public async Task GetRejectsInvalidParametersWithBadRequest(string parameter, string value)
    {
        var controller = BuildController(ScoredS20);

        var result = parameter switch
        {
            "SortBy" => await controller.Get(page: 1, count: 50, sortBy: value),
            "SortDir" => await controller.Get(page: 1, count: 50, sortDir: value),
            "ChartType" => await controller.Get(page: 1, count: 50, chartType: value),
            "MinLetterGrade" => await controller.Get(page: 1, count: 50, minLetterGrade: value),
            _ => await controller.Get(page: 1, count: 50, minPlate: value)
        };

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(parameter, (string)badRequest.Value!);
    }

    // --- Mix parameter (additive, Phoenix 2). The API default is Phoenix permanently — never the
    // --- caller's current on-site mix — so omitting Mix keeps responses byte-identical (the
    // --- untouched golden tests above pin that); these pin the threading and the option gate.

    [Fact]
    public async Task GetOmittingMixRequestsPhoenixRecordsAndCharts()
    {
        var (controller, mediator) = BuildVerifiableController(ScoredS20);

        await controller.Get(page: 1, count: 50);

        mediator.Verify(
            m => m.Send(It.Is<GetPhoenixRecordsQuery>(q => q.Mix == MixEnum.Phoenix),
                It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetWithPhoenix2MixPreservesWireShapeAndThreadsMixIntoQueries()
    {
        var (controller, mediator) = BuildVerifiableController(ScoredS20);

        var result = await controller.Get(page: 1, count: 50, mixString: "Phoenix2");

        // Phoenix2-mix pumbility uses the official Phoenix 2 formula (additive
        // Base x (grade + plate)): S20 SS+ EG = 230 x (1.48 + 0.012) = 343.16.
        // Same wire shape as Phoenix; only this mix's formula differs.
        JsonApproval.AssertWireShape("""
            {
              "page": 1,
              "count": 1,
              "totalResults": 1,
              "results": [
                {
                  "plate": "Extreme Game",
                  "letterGrade": "SS\u002B",
                  "score": 985000,
                  "pumbility": 343.16,
                  "pumbilityPlus": 897,
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
                }
              ]
            }
            """, result);
        mediator.Verify(
            m => m.Send(It.Is<GetPhoenixRecordsQuery>(q => q.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(
            m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("phoenix", MixEnum.Phoenix)]
    [InlineData("phoenix2", MixEnum.Phoenix2)]
    [InlineData("PHOENIX2", MixEnum.Phoenix2)]
    public async Task GetParsesMixCaseInsensitively(string raw, MixEnum expected)
    {
        var (controller, mediator) = BuildVerifiableController(ScoredS20);

        await controller.Get(page: 1, count: 50, mixString: raw);

        mediator.Verify(
            m => m.Send(It.Is<GetPhoenixRecordsQuery>(q => q.Mix == expected), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("xx")]
    [InlineData("Phoenix 2")]
    [InlineData("banana")]
    [InlineData("1")]
    public async Task GetRejectsUnsupportedMixWithOptionsMessage(string mix)
    {
        var (controller, mediator) = BuildVerifiableController(ScoredS20);

        var result = await controller.Get(page: 1, count: 50, mixString: mix);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Mix is invalid, valid values: Phoenix, Phoenix2", (string)badRequest.Value!);
        mediator.Verify(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RecordPhoenixScoreDto RecordScoreBody(string? mix = null)
    {
        return new RecordPhoenixScoreDto
        {
            SongName = "Conflict",
            ChartType = "Single",
            ChartLevel = 20,
            Score = 985000,
            Plate = "ExtremeGame",
            IsBroken = false,
            Mix = mix
        };
    }

    [Fact]
    public async Task RecordScoreOmittingMixLooksUpChartAndRecordsAgainstPhoenix()
    {
        var (controller, mediator) = BuildVerifiableController();
        mediator.Setup(m => m.Send(It.IsAny<GetChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiTestData.Chart1);

        var result = await controller.RecordScore(RecordScoreBody());

        Assert.IsType<OkResult>(result);
        mediator.Verify(
            m => m.Send(It.Is<GetChartQuery>(q => q.Mix == MixEnum.Phoenix), It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(
            m => m.Send(
                It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                    c.Mix == MixEnum.Phoenix && c.ChartId == ApiTestData.ChartId1),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordScoreWithPhoenix2MixThreadsMixIntoChartLookupAndCommand()
    {
        var (controller, mediator) = BuildVerifiableController();
        mediator.Setup(m => m.Send(It.IsAny<GetChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiTestData.Chart1);

        var result = await controller.RecordScore(RecordScoreBody("Phoenix2"));

        Assert.IsType<OkResult>(result);
        mediator.Verify(
            m => m.Send(It.Is<GetChartQuery>(q => q.Mix == MixEnum.Phoenix2), It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(
            m => m.Send(
                It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                    c.Mix == MixEnum.Phoenix2 && c.ChartId == ApiTestData.ChartId1),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("banana")]
    public async Task RecordScoreRejectsUnsupportedMixWithOptionsMessage(string mix)
    {
        var (controller, mediator) = BuildVerifiableController();

        var result = await controller.RecordScore(RecordScoreBody(mix));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Mix is invalid, valid values: Phoenix, Phoenix2", (string)badRequest.Value!);
        mediator.Verify(m => m.Send(It.IsAny<GetChartQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
