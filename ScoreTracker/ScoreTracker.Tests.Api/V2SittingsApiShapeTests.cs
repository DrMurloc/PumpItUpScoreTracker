using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     Pins <c>POST api/v2/players/me/sittings/close</c>, the second write on v2 (docs/design/rise.md
///     D25): the bare 204 a capture app reads as done, answered whether or not anything was open, and
///     the refusals it has to be able to act on.
/// </summary>
public sealed class V2SittingsApiShapeTests
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public V2SittingsApiShapeTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
    }

    private PlayersController Controller(Guid? asTool = null)
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

        return new PlayersController(_mediator.Object, _currentUser.Object, ApiTestClock.Accessor)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static string ProblemType(IActionResult result)
    {
        return ((ProblemDetails)((ObjectResult)result).Value!).Type!;
    }

    private void NothingIsClosed()
    {
        _mediator.Verify(m => m.Send(It.IsAny<CloseOpenSittingsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AClosedSittingAnswersNoContentAndNoBody()
    {
        var result = await Controller().CloseSittings(new CloseSittingsRequestDto("Rise"));

        Assert.Equal(StatusCodes.Status204NoContent, Assert.IsType<NoContentResult>(result).StatusCode);
    }

    [Fact]
    public async Task TheCloseGoesToTheLedgerForTheCallerOnTheNamedMix()
    {
        // Mix names are enum names in any case, as on every v2 call.
        await Controller().CloseSittings(new CloseSittingsRequestDto("riseArcade"));

        _mediator.Verify(m => m.Send(It.Is<CloseOpenSittingsCommand>(c =>
                c.UserId == ApiTestData.PublicUserId && c.Mix == MixEnum.RiseArcade),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AToolCredentialCannotCloseSittings()
    {
        var result = await Controller(ToolId).CloseSittings(new CloseSittingsRequestDto("Rise"));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith("/tool-has-no-self", ProblemType(result));
        NothingIsClosed();
    }

    [Fact]
    public async Task ACloseWithoutABodyIsRefused()
    {
        var result = await Controller().CloseSittings(null);

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith("/body-required", ProblemType(result));
        NothingIsClosed();
    }

    [Theory]
    [InlineData(null, "/mix-required")]
    [InlineData("", "/mix-required")]
    [InlineData("Phoenix 2", "/mix-required")]
    [InlineData("7", "/mix-required")]
    [InlineData("XX", "/legacy-mix")]
    [InlineData("Prime2", "/legacy-mix")]
    public async Task OnlyAPhoenixScoredMixHasSittingsToClose(string? mix, string problem)
    {
        var result = await Controller().CloseSittings(new CloseSittingsRequestDto(mix));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith(problem, ProblemType(result));
        NothingIsClosed();
    }
}
