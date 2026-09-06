using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     Scores on one chart across the players a credential may read. The row is the per-player
///     score row with the player's identity in front of it, and the page never names a player who
///     did not share.
/// </summary>
public sealed class V2ChartScoresApiShapeTests
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly Guid ThirdUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public V2ChartScoresApiShapeTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1, ApiTestData.Chart2 });
        _mediator.Setup(m => m.Send(It.IsAny<GetUsersByIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUsersByIdsQuery q, CancellationToken _) =>
                q.UserIds.Select(UserFor).ToArray());
        // Phoenix 2 links the public user; only Phoenix links the private one — the fallback case.
        _mediator.Setup(m => m.Send(It.IsAny<GetLinkedOfficialPlayerTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetLinkedOfficialPlayerTagsQuery q, CancellationToken _) =>
                (q.Mix == MixEnum.Phoenix2
                    ? new Dictionary<Guid, string> { [ApiTestData.PublicUserId] = "VISIBL" }
                    : new Dictionary<Guid, string> { [ApiTestData.PrivateUserId] = "HIDDEN" })
                .Where(kv => q.UserIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value));
        _mediator.Setup(m => m.Send(It.IsAny<GetChartRecordsForPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerChartRecord>());
    }

    private static User UserFor(Guid id)
    {
        if (id == ApiTestData.PublicUserId) return ApiTestData.PublicUser;
        if (id == ApiTestData.PrivateUserId) return ApiTestData.PrivateUser;
        return new User(id, Name.From("Third"), true, null, new Uri("https://piuimages.example.com/avatar3.png"), null);
    }

    private ChartScoresController Controller(Guid? asTool)
    {
        var context = new DefaultHttpContext
        {
            Request = { Scheme = "https", Host = new HostString("piu") }
        };
        if (asTool is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ToolKeyAuthenticationScheme.ToolIdClaim, asTool.Value.ToString())
            }, "ApiV2"));

        return new ChartScoresController(_mediator.Object, _currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private void SetupPool(params Guid[] readable)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetToolReadablePlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readable);
    }

    private void SetupRecords(params PlayerChartRecord[] records)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartRecordsForPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
    }

    private static PlayerChartRecord Best(Guid userId, int score, PhoenixPlate? plate = PhoenixPlate.SuperbGame,
        bool broken = false)
    {
        return new PlayerChartRecord(userId, new RecordedPhoenixScore(ApiTestData.ChartId1, PhoenixScore.From(score),
            broken ? null : plate, broken, ApiTestData.Date1, "officialImport"));
    }

    [Fact]
    public async Task RowIsThePlayerScoreRowWithIdentityInFront()
    {
        SetupPool(ApiTestData.PublicUserId);
        SetupRecords(new PlayerChartRecord(ApiTestData.PublicUserId, new RecordedPhoenixScore(ApiTestData.ChartId1,
            PhoenixScore.From(978210), PhoenixPlate.MarvelousGame, false, ApiTestData.Date1, "officialImport",
            new JudgementCounts(1013, 4, 0, 0, 1, 1016))));

        var result = await Controller(ToolId).GetScores(ApiTestData.ChartId1, "Phoenix");

        // The formula's own answers, so the golden pins the shape and not a number retyped by hand.
        var score = PhoenixScore.From(978210);
        var pumbility = Math.Round(ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, true)
            .GetScore(ApiTestData.Chart1, score, PhoenixPlate.MarvelousGame, false), 2);
        JsonApproval.AssertWireShape("""
            {
              "mix": "Phoenix",
              "scoringModel": "phoenix",
              "data": [
                {
                  "userId": "33333333-3333-3333-3333-333333333333",
                  "username": "VisiblePlayer",
                  "gameTag": "VISIBL",
                  "chartId": "11111111-1111-1111-1111-111111111111",
                  "recordedAt": "2026-01-15T00:00:00+00:00",
                  "source": "officialImport",
                  "score": 978210,
                  "letterGrade": "__GRADE__",
                  "plate": "Marvelous Game",
                  "isBroken": false,
                  "pumbility": __PUMBILITY__,
                  "judgments": {
                    "perfects": 1013,
                    "greats": 4,
                    "goods": 0,
                    "bads": 0,
                    "misses": 1,
                    "maxCombo": 1016
                  }
                }
              ],
              "limit": 100,
              "total": 1,
              "next": null
            }
            """
            // Serialized with the wire's own options: a "+" in a grade travels as +.
            .Replace("\"__GRADE__\"", JsonSerializer.Serialize(score.LetterGradeFor(MixEnum.Phoenix).GetName(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .Replace("__PUMBILITY__", JsonSerializer.Serialize(pumbility)), result);
    }

    [Fact]
    public async Task PassesRankAboveFailedBestsThenByScoreThenByPlayer()
    {
        SetupPool(ApiTestData.PublicUserId, ApiTestData.PrivateUserId, ThirdUserId);
        SetupRecords(
            Best(ApiTestData.PublicUserId, 950000),
            Best(ThirdUserId, 999000, broken: true),
            Best(ApiTestData.PrivateUserId, 990000));

        var result = await Controller(ToolId).GetScores(ApiTestData.ChartId1, "Phoenix");

        var page = Assert.IsType<ChartScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(new[] { ApiTestData.PrivateUserId, ApiTestData.PublicUserId, ThirdUserId },
            page.Data.Select(r => r.UserId).ToArray());
        Assert.Equal(3, page.Total);
        Assert.True(page.Data[2].IsBroken);
        Assert.Null(page.Data[2].Plate);
    }

    /// <summary>
    ///     The share gate is the id list handed to the ledger: a tool's pool, or the caller alone.
    ///     Nobody outside it is ever asked for, so nobody outside it can be on the page.
    /// </summary>
    [Fact]
    public async Task AToolAsksForItsPoolAndAPersonalTokenForItselfAlone()
    {
        SetupPool(ApiTestData.PrivateUserId, ThirdUserId);

        await Controller(ToolId).GetScores(ApiTestData.ChartId1, "Phoenix");
        _mediator.Verify(m => m.Send(It.Is<GetChartRecordsForPlayersQuery>(q =>
                q.UserIds.Count == 2 && q.UserIds.Contains(ApiTestData.PrivateUserId) && q.UserIds.Contains(ThirdUserId)),
            It.IsAny<CancellationToken>()), Times.Once);

        await Controller(null).GetScores(ApiTestData.ChartId1, "Phoenix");
        _mediator.Verify(m => m.Send(It.Is<GetChartRecordsForPlayersQuery>(q =>
                q.UserIds.Count == 1 && q.UserIds.Contains(ApiTestData.PublicUserId)),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<GetToolReadablePlayersQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GameTagFallsBackToThePhoenixLinkWhenPhoenix2HasNone()
    {
        SetupPool(ApiTestData.PrivateUserId);
        SetupRecords(Best(ApiTestData.PrivateUserId, 990000));

        var result = await Controller(ToolId).GetScores(ApiTestData.ChartId1, "Phoenix");

        var page = Assert.IsType<ChartScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        var row = Assert.Single(page.Data);
        Assert.Equal("HiddenPlayer", row.Username);
        Assert.Equal("HIDDEN", row.GameTag);
    }

    [Fact]
    public async Task LegacyMixRowsCarryTheLegacyModelAndNoPumbility()
    {
        SetupPool(ApiTestData.PublicUserId);
        SetupRecords(Best(ApiTestData.PublicUserId, 950000));

        var result = await Controller(ToolId).GetScores(ApiTestData.ChartId1, "FiestaEx");

        var page = Assert.IsType<ChartScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("legacy", page.ScoringModel);
        Assert.Null(Assert.Single(page.Data).Pumbility);
    }

    [Fact]
    public async Task AChartNotInTheMixIs404AndMixIsRequired()
    {
        SetupPool(ApiTestData.PublicUserId);

        var missing = await Controller(ToolId).GetScores(Guid.NewGuid(), "Phoenix");
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(missing).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);

        var noMix = await Controller(ToolId).GetScores(ApiTestData.ChartId1);
        var mixProblem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(noMix).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/mix-required", mixProblem.Type);
    }

    [Fact]
    public async Task PagesWalkTheWholeListAndACursorFromAnotherChartIsRejected()
    {
        SetupPool(ApiTestData.PublicUserId, ApiTestData.PrivateUserId, ThirdUserId);
        SetupRecords(Best(ApiTestData.PublicUserId, 950000), Best(ThirdUserId, 999000),
            Best(ApiTestData.PrivateUserId, 990000));

        var first = Assert.IsType<ChartScorePageDto>(Assert.IsType<JsonResult>(
            await Controller(ToolId).GetScores(ApiTestData.ChartId1, "Phoenix", limit: 2)).Value);
        Assert.Equal(2, first.Data.Length);
        Assert.NotNull(first.Next);
        var cursor = Uri.UnescapeDataString(first.Next!.Split("cursor=")[1]);

        var second = Assert.IsType<ChartScorePageDto>(Assert.IsType<JsonResult>(
            await Controller(ToolId).GetScores(ApiTestData.ChartId1, "Phoenix", cursor, 2)).Value);
        Assert.Equal(ApiTestData.PublicUserId, Assert.Single(second.Data).UserId);
        Assert.Null(second.Next);

        var elsewhere = await Controller(ToolId).GetScores(ApiTestData.ChartId2, "Phoenix", cursor, 2);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(elsewhere).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/invalid-cursor", problem.Type);
    }
}
