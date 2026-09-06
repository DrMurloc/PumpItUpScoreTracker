using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Controllers;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The retired March of Murlocs routes keep resolving (§11.1): the directory to the season page,
///     a board to its season with that board selected, and a stamina id that is not a MoM board to
///     that tournament's own page.
/// </summary>
public sealed class MoMRedirectControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private MoMRedirectController Controller() => new(_mediator.Object);

    [Fact]
    public void TheDirectoryIsTheSeasonPageNow()
    {
        var result = Assert.IsType<RedirectResult>(Controller().Directory());
        Assert.Equal("/MarchOfMurlocs", result.Url);
        Assert.True(result.Permanent);
    }

    [Fact]
    public async Task ALiveBoardLandsOnTheLiveSeasonWithItsBoardSelected()
    {
        var board = Guid.NewGuid();
        _mediator.Setup(m => m.Send(new GetMoMBoardLocatorQuery(board), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMBoardLocator(Guid.NewGuid(), ChartType.Single, MixEnum.Phoenix, true));

        var result = Assert.IsType<RedirectResult>(await Controller().Board(board, CancellationToken.None));

        Assert.Equal("/MarchOfMurlocs?board=Single", result.Url);
        Assert.True(result.Permanent);
    }

    [Fact]
    public async Task APastBoardLandsOnItsOwnSeasonPage()
    {
        var board = Guid.NewGuid();
        var season = Guid.NewGuid();
        _mediator.Setup(m => m.Send(new GetMoMBoardLocatorQuery(board), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoMBoardLocator(season, ChartType.Double, MixEnum.Phoenix, false));

        var result = Assert.IsType<RedirectResult>(await Controller().Board(board, CancellationToken.None));

        Assert.Equal($"/MarchOfMurlocs/{season}?board=Double", result.Url);
    }

    [Fact]
    public async Task AStaminaIdThatIsNotAMoMBoardLandsOnThatTournamentsPage()
    {
        var id = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetMoMBoardLocatorQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMBoardLocator?)null);

        var result = Assert.IsType<RedirectResult>(await Controller().Board(id, CancellationToken.None));

        Assert.Equal($"/Tournament/{id}/Qualifiers", result.Url);
        Assert.True(result.Permanent);
    }
}
