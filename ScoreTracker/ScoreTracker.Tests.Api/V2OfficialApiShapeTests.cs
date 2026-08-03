using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The official-board reads. Public piugame data, no share required — and no link from a piugame
///     tag to a PIU Scores account.
/// </summary>
public sealed class V2OfficialApiShapeTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly OfficialController _controller;

    private static readonly OfficialPlayerRecord Player = new(88213, "MURLOC#1",
        new Uri("https://piuimages.example.com/avatar.png"), ApiTestData.PublicUserId);

    public V2OfficialApiShapeTests()
    {
        _controller = new OfficialController(_mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { Request = { Scheme = "https", Host = new HostString("piu") } }
            }
        };
    }

    [Fact]
    public async Task RankingsCarryWeekDeltasAndArchetypeButNoSiteUserId()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialRankingsRecord(ApiTestData.Date1, true, new[]
            {
                new OfficialRankingRecord(1, 2, Player, 1043.25m, 54, null)
            }));

        var result = await _controller.GetRankings("Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "snapshotAt": "2026-01-15T00:00:00+00:00",
              "ratingIsOfficial": true,
              "data": [
                {
                  "rank": 1,
                  "previousRank": 2,
                  "player": {
                    "playerId": 88213,
                    "gameTag": "MURLOC#1",
                    "avatarUrl": "https://piuimages.example.com/avatar.png"
                  },
                  "rating": 1043.25,
                  "boardsInTop": 54,
                  "playerType": null
                }
              ]
            }
            """, result);
    }

    [Fact]
    public async Task ChartBoardIsPlaceTagAndScore()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialChartBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialChartBoardRecord(ApiTestData.Date1, new[]
            {
                new OfficialChartBoardEntryRecord(1, Player, 998430)
            }));

        var result = await _controller.GetChartBoard(ApiTestData.ChartId1, "Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "asOf": "2026-01-15T00:00:00+00:00",
              "data": [
                {
                  "place": 1,
                  "player": {
                    "playerId": 88213,
                    "gameTag": "MURLOC#1",
                    "avatarUrl": "https://piuimages.example.com/avatar.png"
                  },
                  "score": 998430
                }
              ]
            }
            """, result);
    }

    [Fact]
    public async Task PopularityCarriesTheTrendWindow()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPopularityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new OfficialPopularityRecord(ApiTestData.ChartId1, 3, 5, new[] { 5, 5, 4, 4, 6, 3, 3, 3 })
            });

        var result = await _controller.GetPopularity("Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "chartId": "11111111-1111-1111-1111-111111111111",
                  "place": 3,
                  "previousPlace": 5,
                  "recentPlaces": [
                    5,
                    5,
                    4,
                    4,
                    6,
                    3,
                    3,
                    3
                  ]
                }
              ],
              "limit": 1,
              "total": 1,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task UnmirroredChartBoardIs404()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialChartBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfficialChartBoardRecord?)null);

        var result = await _controller.GetChartBoard(ApiTestData.ChartId1, "Phoenix");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task PlayerProfileTagIsTheOnlyIdentityReturned()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPlayerProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialPlayerProfileRecord(Player, null, 1043.25m, 1, 1, 54, 3, 1, 19,
                Array.Empty<OfficialPlayerHistoryPoint>(),
                new[] { new OfficialPlayerChartRecord(ApiTestData.ChartId1, 4, -1, 994120, 1021) }));

        var result = await _controller.GetPlayer("MURLOC#1", "Phoenix");

        var dto = Assert.IsType<OfficialPlayerProfileDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("MURLOC#1", dto.Player.GameTag);
        Assert.Equal(88213, dto.Player.PlayerId);
    }

    /// <summary>
    ///     The strong form of the rule, because a shape assertion only covers the DTOs that exist
    ///     today. Every official DTO is scanned for anything that could carry a PIU Scores account
    ///     id — a new one added later fails here rather than leaking quietly.
    /// </summary>
    [Fact]
    public void NoOfficialDtoExposesASiteAccountId()
    {
        var offenders = typeof(OfficialPlayerDto).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(OfficialPlayerDto).Namespace && t.Name.StartsWith("Official"))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?))
                .Where(p => p.Name.Contains("User", StringComparison.OrdinalIgnoreCase)
                            || p.Name.Equals("AccountId", StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task MixIsRequiredOnEveryOfficialRead()
    {
        foreach (var result in new[]
                 {
                     await _controller.GetRankings(),
                     await _controller.GetPlayer("MURLOC#1"),
                     await _controller.GetChartBoard(ApiTestData.ChartId1),
                     await _controller.GetPopularity(),
                     await _controller.GetWhatItTakes(),
                     await _controller.GetWeeklyHighlights()
                 })
        {
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
            Assert.Equal("https://piuscores.arroweclip.se/errors/mix-required", problem.Type);
        }
    }
}
