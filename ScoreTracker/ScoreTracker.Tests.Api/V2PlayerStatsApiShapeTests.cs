using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     A player's PUMBILITY numbers — the one thing no <c>/players</c> read carried before. One
///     object per player, the same on the single read and in the bulk, share-gated like everything
///     else under <c>/players</c>.
/// </summary>
public sealed class V2PlayerStatsApiShapeTests
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly Guid MakerId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
    private static readonly Guid ThirdUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset BoardDate = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public V2PlayerStatsApiShapeTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        _mediator.Setup(m => m.Send(It.IsAny<GetUsersByIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUsersByIdsQuery q, CancellationToken _) => q.UserIds.Select(UserFor).ToArray());
        _mediator.Setup(m => m.Send(It.IsAny<GetLinkedOfficialPlayerTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetLinkedOfficialPlayerTagsQuery q, CancellationToken _) =>
                q.Mix == MixEnum.Phoenix2 && q.UserIds.Contains(ApiTestData.PublicUserId)
                    ? new Dictionary<Guid, string> { [ApiTestData.PublicUserId] = "VISIBL" }
                    : new Dictionary<Guid, string>());
        _mediator.Setup(m => m.Send(It.IsAny<GetToolReadablePlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.PublicUserId, ApiTestData.PrivateUserId, ThirdUserId });
        _mediator.Setup(m => m.Send(It.IsAny<CanToolReadPlayerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CanToolReadPlayerQuery q, CancellationToken _) => q.UserId != ThirdUserId);
        _mediator.Setup(m => m.Send(It.IsAny<GetToolOwnerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakerId);
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayersStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerStatsRecord>());
    }

    private static User UserFor(Guid id)
    {
        if (id == ApiTestData.PublicUserId) return ApiTestData.PublicUser;
        if (id == ApiTestData.PrivateUserId) return ApiTestData.PrivateUser;
        return new User(id, Name.From("Third"), true, null, new Uri("https://piuimages.example.com/avatar3.png"), null);
    }

    private static PlayerStatsRecord Stats(Guid userId, double pumbility, int? rank = 812)
    {
        return new PlayerStatsRecord(userId, 0, 24, 1532, 1234.5, 0, pumbility, 0, 0, 9000.123, 0, 0, 8173.171,
            0, 0, 21.336, 21.1, 20.9, rank, null, null, BoardDate);
    }

    private PlayersController Controller(Guid? asTool)
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

        return new PlayersController(_mediator.Object, _currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private void SetupStats(params PlayerStatsRecord[] stats)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayersStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetPlayersStatsQuery q, CancellationToken _) =>
                stats.Where(s => q.UserIds.Contains(s.UserId)).ToArray());
    }

    [Fact]
    public async Task StatsAreThePumbilityPageNumbersRoundedForPresentation()
    {
        SetupStats(Stats(ApiTestData.PublicUserId, 17173.291));

        var result = await Controller(null).GetPlayerStats("me", "Phoenix2");

        JsonApproval.AssertWireShape("""
            {
              "userId": "33333333-3333-3333-3333-333333333333",
              "username": "VisiblePlayer",
              "gameTag": "VISIBL",
              "pumbility": 17173.29,
              "singlesPumbility": 9000.12,
              "doublesPumbility": 8173.17,
              "coOpPumbility": 1234.5,
              "competitiveLevel": 21.34,
              "singlesCompetitiveLevel": 21.1,
              "doublesCompetitiveLevel": 20.9,
              "highestLevel": 24,
              "clearCount": 1532,
              "estimatedPumbilityRank": 812,
              "estimatedSinglesPumbilityRank": null,
              "estimatedDoublesPumbilityRank": null,
              "estimatedRankAsOf": "2026-08-30T00:00:00+00:00"
            }
            """, result);
    }

    [Fact]
    public async Task TheBulkRanksByPumbilityCarriesTheTotalAndSkipsPlayersWithoutARecord()
    {
        // The third user shared but has no record in the mix.
        SetupStats(Stats(ApiTestData.PublicUserId, 15000), Stats(ApiTestData.PrivateUserId, 17000, null));

        var result = await Controller(ToolId).GetStats("Phoenix2");

        var page = Assert.IsType<CursorPageDto<PlayerStatsDto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(new[] { ApiTestData.PrivateUserId, ApiTestData.PublicUserId },
            page.Data.Select(r => r.UserId).ToArray());
        Assert.Equal(2, page.Total);
        Assert.Equal("HiddenPlayer", page.Data[0].Username);
        Assert.Null(page.Data[0].GameTag);
        Assert.Null(page.Data[0].EstimatedPumbilityRank);
    }

    [Fact]
    public async Task ASharedPlayerWithoutARecordIs404OnTheSingleRead()
    {
        var result = await Controller(ToolId).GetPlayerStats(ApiTestData.PrivateUserId.ToString(), "Phoenix2");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    /// <summary>
    ///     The same 404 an unshared player gets everywhere under /players: the tool must not learn
    ///     that the account exists, let alone its number.
    /// </summary>
    [Fact]
    public async Task AnUnsharedPlayerIs404AndTheirStatsAreNeverRead()
    {
        SetupStats(Stats(ThirdUserId, 19000));

        var result = await Controller(ToolId).GetPlayerStats(ThirdUserId.ToString(), "Phoenix2");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        _mediator.Verify(m => m.Send(It.IsAny<GetPlayersStatsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ALegacyMixIs404OnBothReadsBeforeAnythingIsRead()
    {
        var single = await Controller(null).GetPlayerStats("me", "FiestaEx");
        var bulk = await Controller(ToolId).GetStats("FiestaEx");

        foreach (var result in new[] { single, bulk })
        {
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Contains("PUMBILITY", problem.Detail);
        }

        _mediator.Verify(m => m.Send(It.IsAny<GetPlayersStatsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheBulkAppliesTheCommunityFilterBeforeItReadsAnyStats()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMembersForViewerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { ApiTestData.PrivateUserId, Guid.NewGuid() });
        SetupStats(Stats(ApiTestData.PublicUserId, 15000), Stats(ApiTestData.PrivateUserId, 17000));

        var result = await Controller(ToolId).GetStats("Phoenix2", community: "Canada");

        var page = Assert.IsType<CursorPageDto<PlayerStatsDto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(ApiTestData.PrivateUserId, Assert.Single(page.Data).UserId);
        _mediator.Verify(m => m.Send(It.Is<GetPlayersStatsQuery>(q =>
                q.UserIds.Count() == 1 && q.UserIds.Contains(ApiTestData.PrivateUserId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     <c>players/stats</c> lives beside <c>players/{playerId}</c>. Attribute routing prefers a
    ///     literal segment over a parameter, so the bulk read wins that URL — as long as the
    ///     template stays a literal. This pins the templates the precedence rests on.
    /// </summary>
    [Fact]
    public void TheBulkStatsRouteIsALiteralBesideThePlayerIdRoute()
    {
        static string Template(string action) => typeof(PlayersController).GetMethod(action)!
            .GetCustomAttribute<HttpGetAttribute>()!.Template!;

        Assert.Equal("stats", Template(nameof(PlayersController.GetStats)));
        Assert.Equal("{playerId}", Template(nameof(PlayersController.GetPlayer)));
        Assert.Equal("{playerId}/stats", Template(nameof(PlayersController.GetPlayerStats)));
    }
}
