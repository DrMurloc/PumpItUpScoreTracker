using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The players list a tool crawls to learn who shared with it: one row per player, in the order
///     the cursor counts through, each carrying one game tag — the newer Phoenix mix's link first —
///     and every page read in bulk rather than a player at a time.
/// </summary>
public sealed class V2PlayersListApiShapeTests
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly Guid ThirdUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid VanishedUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static readonly User ThirdUser = new(ThirdUserId, Name.From("Third"), true, null,
        new Uri("https://piuimages.example.com/avatar3.png"), null);

    // The public player is linked on both mixes, the private one on Phoenix only, and the third
    // player's Phoenix 2 link carries a blank tag.
    private static readonly Dictionary<(MixEnum Mix, Guid UserId), string> Tags = new()
    {
        [(MixEnum.Phoenix2, ApiTestData.PublicUserId)] = "VISIBLE#1234",
        [(MixEnum.Phoenix, ApiTestData.PublicUserId)] = "OLDTAG#0001",
        [(MixEnum.Phoenix, ApiTestData.PrivateUserId)] = "HIDDEN#5678",
        [(MixEnum.Phoenix2, ThirdUserId)] = ""
    };

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public V2PlayersListApiShapeTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        // The bulk read hands accounts back in its own order — here, the reverse of the page.
        _mediator.Setup(m => m.Send(It.IsAny<GetUsersByIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUsersByIdsQuery q, CancellationToken _) => q.UserIds
                .OrderByDescending(id => id)
                .Select(UserFor)
                .OfType<User>()
                .ToArray());
        _mediator.Setup(m => m.Send(It.IsAny<GetLinkedOfficialPlayerTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetLinkedOfficialPlayerTagsQuery q, CancellationToken _) => q.UserIds
                .Where(id => Tags.ContainsKey((q.Mix, id)))
                .ToDictionary(id => id, id => Tags[(q.Mix, id)]));
    }

    private static User? UserFor(Guid id)
    {
        if (id == ApiTestData.PublicUserId) return ApiTestData.PublicUser;
        if (id == ApiTestData.PrivateUserId) return ApiTestData.PrivateUser;
        return id == ThirdUserId ? ThirdUser : null;
    }

    private PlayersController Controller(params Guid[] readable)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetToolReadablePlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readable);
        var context = new DefaultHttpContext
        {
            Request = { Scheme = "https", Host = new HostString("piu") },
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ToolKeyAuthenticationScheme.ToolIdClaim, ToolId.ToString())
            }, "ApiV2"))
        };

        return new PlayersController(_mediator.Object, _currentUser.Object, ApiTestClock.Accessor)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [Fact]
    public async Task EachRowCarriesTheProfileAndOneTagNewestMixFirst()
    {
        var result = await Controller(ThirdUserId, ApiTestData.PrivateUserId, ApiTestData.PublicUserId)
            .GetPlayers();

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "userId": "33333333-3333-3333-3333-333333333333",
                  "username": "VisiblePlayer",
                  "country": "Canada",
                  "avatarUrl": "https://piuimages.example.com/avatar1.png",
                  "isPublic": true,
                  "gameTag": "VISIBLE#1234"
                },
                {
                  "userId": "44444444-4444-4444-4444-444444444444",
                  "username": "HiddenPlayer",
                  "country": null,
                  "avatarUrl": "https://piuimages.example.com/avatar2.png",
                  "isPublic": false,
                  "gameTag": "HIDDEN#5678"
                },
                {
                  "userId": "66666666-6666-6666-6666-666666666666",
                  "username": "Third",
                  "country": null,
                  "avatarUrl": "https://piuimages.example.com/avatar3.png",
                  "isPublic": true,
                  "gameTag": null
                }
              ],
              "limit": 100,
              "total": 3,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task AnAccountThatNoLongerExistsIsLeftOutOfThePage()
    {
        var result = await Controller(ApiTestData.PublicUserId, VanishedUserId).GetPlayers();

        var page = Assert.IsType<CursorPageDto<PlayerV2Dto>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(ApiTestData.PublicUserId, Assert.Single(page.Data).UserId);
    }

    /// <summary>
    ///     One read for the accounts and one per mix for the tags, however long the page: a read per
    ///     row puts hundreds of round trips behind a single response.
    /// </summary>
    [Fact]
    public async Task APageIsReadInBulkRatherThanAPlayerAtATime()
    {
        await Controller(ThirdUserId, ApiTestData.PrivateUserId, ApiTestData.PublicUserId).GetPlayers();

        _mediator.Verify(m => m.Send(It.IsAny<GetUsersByIdsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<GetLinkedOfficialPlayerTagsQuery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _mediator.Verify(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<GetLinkedOfficialPlayerTagQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
