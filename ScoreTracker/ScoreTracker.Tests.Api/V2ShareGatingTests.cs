using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The authorization model: who a credential can reach, and what an unreachable player looks
///     like. Getting this wrong is the difference between a share and a data leak.
/// </summary>
public sealed class V2ShareGatingTests
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public V2ShareGatingTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<RecordedPhoenixScore>());
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiTestData.PublicUser);
    }

    /// <param name="asTool">null builds a personal-token caller; a value builds a tool caller.</param>
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

    private void SetupShare(bool canRead, params Guid[] readable)
    {
        _mediator.Setup(m => m.Send(It.IsAny<CanToolReadPlayerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canRead);
        _mediator.Setup(m => m.Send(It.IsAny<GetToolReadablePlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readable);
    }

    [Fact]
    public async Task AToolReachesAPlayerThatSharedWithIt()
    {
        SetupShare(true, ApiTestData.PublicUserId);

        var result = await Controller(ToolId).GetPlayer(ApiTestData.PublicUserId.ToString());

        var dto = Assert.IsType<PlayerV2Dto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(ApiTestData.PublicUserId, dto.UserId);
    }

    // 404, not 403. A 403 confirms the account exists, which turns the endpoint into an
    // enumeration oracle for anyone holding any key.
    [Fact]
    public async Task APlayerThatDidNotShareIsNotFoundRatherThanForbidden()
    {
        SetupShare(false);

        var result = await Controller(ToolId).GetPlayer(ApiTestData.PrivateUserId.ToString());

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("https://piuscores.arroweclip.se/errors/not-found", problem.Type);
    }

    [Fact]
    public async Task TheShareGateGuardsScoresAndJournalToNotJustTheProfile()
    {
        SetupShare(false);
        var controller = Controller(ToolId);

        foreach (var result in new[]
                 {
                     await controller.GetScores(ApiTestData.PrivateUserId.ToString(), "Phoenix"),
                     await controller.GetSessions(ApiTestData.PrivateUserId.ToString(), "Phoenix"),
                     await controller.GetJournal(ApiTestData.PrivateUserId.ToString(), "Phoenix")
                 })
        {
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        }
    }

    // A tool is not a player, so "me" has no referent — better to say so than to resolve it to
    // something surprising.
    [Fact]
    public async Task AToolCannotAddressItselfAsMe()
    {
        SetupShare(true, ApiTestData.PublicUserId);

        var result = await Controller(ToolId).GetPlayer("me");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/tool-has-no-self", problem.Type);
    }

    [Fact]
    public async Task APersonalTokenStillReachesOnlyItself()
    {
        var controller = Controller(null);

        var mine = await controller.GetPlayer("me");
        var theirs = await controller.GetPlayer(ApiTestData.PrivateUserId.ToString());

        Assert.IsType<JsonResult>(mine);
        Assert.Equal(StatusCodes.Status404NotFound,
            Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(theirs).Value).Status);
    }

    // Without this a tool has no way to learn who consented.
    [Fact]
    public async Task ToolListsExactlyThePlayersThatSharedWithIt()
    {
        SetupShare(true, ApiTestData.PublicUserId);

        var result = await Controller(ToolId).GetPlayers();

        var page = Assert.IsType<CursorPageDto<PlayerV2Dto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(ApiTestData.PublicUserId, Assert.Single(page.Data).UserId);
    }

    [Fact]
    public async Task APersonalTokenListsOnlyItself()
    {
        var result = await Controller(null).GetPlayers();

        var page = Assert.IsType<CursorPageDto<PlayerV2Dto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(ApiTestData.PublicUserId, Assert.Single(page.Data).UserId);
        _mediator.Verify(m => m.Send(It.IsAny<GetToolReadablePlayersQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
