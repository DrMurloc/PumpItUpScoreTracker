using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The community filter on the players list. It narrows the players a credential may already
///     read to a community's members, and it borrows the site's own rule for who may see a
///     private roster — so it can never widen a tool's reach, and it cannot be used to find out
///     which private communities exist.
/// </summary>
public sealed class V2CommunityFilterTests
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly Guid MakerId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
    private static readonly Guid StrangerId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public V2CommunityFilterTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUserByIdQuery q, CancellationToken _) =>
                q.UserId == ApiTestData.PrivateUserId ? ApiTestData.PrivateUser : ApiTestData.PublicUser);
        _mediator.Setup(m => m.Send(It.IsAny<GetToolReadablePlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.PublicUserId, ApiTestData.PrivateUserId });
        _mediator.Setup(m => m.Send(It.IsAny<GetToolOwnerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakerId);
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

    private void SetupMembers(IReadOnlySet<Guid>? members)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMembersForViewerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);
    }

    [Fact]
    public async Task TheFilterNarrowsAToolsPoolToTheCommunitysMembersAndNeverWidensIt()
    {
        // The private user shared and is a member; the stranger is a member who never shared.
        SetupMembers(new HashSet<Guid> { ApiTestData.PrivateUserId, StrangerId });

        var result = await Controller(ToolId).GetPlayers(community: "Canada");

        var page = Assert.IsType<CursorPageDto<PlayerV2Dto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(ApiTestData.PrivateUserId, Assert.Single(page.Data).UserId);
        Assert.Equal(1, page.Total);
    }

    /// <summary>The viewer whose membership decides a private community is the tool's maker, not the tool.</summary>
    [Fact]
    public async Task AToolViewsACommunityAsItsMakerAndAPersonalTokenAsItself()
    {
        SetupMembers(new HashSet<Guid> { ApiTestData.PublicUserId });

        await Controller(ToolId).GetPlayers(community: "Acme");
        _mediator.Verify(m => m.Send(It.Is<GetCommunityMembersForViewerQuery>(q =>
                q.ViewerUserId == MakerId && (string)q.CommunityName == "Acme"),
            It.IsAny<CancellationToken>()), Times.Once);

        await Controller(null).GetPlayers(community: "Acme");
        _mediator.Verify(m => m.Send(It.Is<GetCommunityMembersForViewerQuery>(q =>
                q.ViewerUserId == ApiTestData.PublicUserId),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<GetToolOwnerQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A private community the viewer is not in and a name that matches nothing get the same
    ///     answer, because the Communities read gives them the same null — the filter has no way to
    ///     tell them apart, so neither has a caller.
    /// </summary>
    [Fact]
    public async Task AHiddenOrUnknownCommunityIs404()
    {
        SetupMembers(null);

        var result = await Controller(ToolId).GetPlayers(community: "Nowhere");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("https://piuscores.arroweclip.se/errors/not-found", problem.Type);
    }

    [Fact]
    public async Task ABlankCommunityNameIs400()
    {
        var result = await Controller(ToolId).GetPlayers(community: "   ");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("https://piuscores.arroweclip.se/errors/invalid-community", problem.Type);
        _mediator.Verify(m => m.Send(It.IsAny<GetCommunityMembersForViewerQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WithoutTheFilterTheListIsTheWholePoolAndNoCommunityReadHappens()
    {
        var result = await Controller(ToolId).GetPlayers();

        var page = Assert.IsType<CursorPageDto<PlayerV2Dto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(2, page.Data.Length);
        _mediator.Verify(m => m.Send(It.IsAny<GetCommunityMembersForViewerQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>A cursor is bound to the filter it was issued under; the same page under another community is refused.</summary>
    [Fact]
    public async Task ACursorFromAnotherCommunityIsRejected()
    {
        SetupMembers(new HashSet<Guid> { ApiTestData.PublicUserId, ApiTestData.PrivateUserId });

        var first = Assert.IsType<CursorPageDto<PlayerV2Dto>>(Assert.IsType<JsonResult>(
            await Controller(ToolId).GetPlayers(community: "Canada", limit: 1)).Value);
        Assert.NotNull(first.Next);
        var cursor = Uri.UnescapeDataString(first.Next!.Split("cursor=")[1]);

        var elsewhere = await Controller(ToolId).GetPlayers(community: "Chile", cursor: cursor, limit: 1);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(elsewhere).Value);
        Assert.Equal("https://piuscores.arroweclip.se/errors/invalid-cursor", problem.Type);
    }
}
