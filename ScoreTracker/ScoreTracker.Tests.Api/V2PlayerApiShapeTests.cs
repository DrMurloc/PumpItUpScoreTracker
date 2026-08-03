using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     A player's own reads on api/v2. Only "me" resolves until share-gating lands.
/// </summary>
public sealed class V2PlayerApiShapeTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly PlayersController _controller;

    public V2PlayerApiShapeTests()
    {
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        _controller = new PlayersController(_mediator.Object, currentUser.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { Request = { Scheme = "https", Host = new HostString("piu") } }
            }
        };
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1 });
    }

    private void SetupScores(params RecordedPhoenixScore[] scores)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);
    }

    [Fact]
    public async Task ScorePageCarriesTheScoringModelAndJudgmentCounts()
    {
        SetupScores(new RecordedPhoenixScore(ApiTestData.ChartId1, PhoenixScore.From(978210),
            PhoenixPlate.MarvelousGame, false, ApiTestData.Date1, "officialImport",
            new JudgementCounts(1013, 4, 0, 0, 1)));

        var result = await _controller.GetScores("me", "Phoenix");

        var page = Assert.IsType<PlayerScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("Phoenix", page.Mix);
        Assert.Equal("phoenix", page.ScoringModel);
        var row = Assert.Single(page.Data);
        Assert.Equal(978210, row.Score);
        Assert.Equal("Marvelous Game", row.Plate);
        Assert.Equal(1013, row.Judgments!.Perfects);
        Assert.Equal(1, row.Judgments.Misses);
    }

    // Zeros would read as a perfect game, so a source that never carried judgments reports none.
    [Fact]
    public async Task ScoreWithoutJudgmentsReportsNullRatherThanZeros()
    {
        SetupScores(new RecordedPhoenixScore(ApiTestData.ChartId1, PhoenixScore.From(978210),
            PhoenixPlate.MarvelousGame, false, ApiTestData.Date1, "csv"));

        var result = await _controller.GetScores("me", "Phoenix");

        var page = Assert.IsType<PlayerScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Null(Assert.Single(page.Data).Judgments);
    }

    [Fact]
    public async Task LegacyMixReportsTheLegacyScoringModel()
    {
        SetupScores();

        var result = await _controller.GetScores("me", "FiestaEx");

        var page = Assert.IsType<PlayerScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("legacy", page.ScoringModel);
    }

    // PUMBILITY has no formula outside the Phoenix mixes; asking for one throws. A legacy row
    // reports no rating rather than a fabricated number.
    [Fact]
    public async Task LegacyMixScoresCarryNoPumbilityRatherThanThrowing()
    {
        SetupScores(new RecordedPhoenixScore(ApiTestData.ChartId1, PhoenixScore.From(874350),
            null, false, ApiTestData.Date1, "manual"));

        var result = await _controller.GetScores("me", "FiestaEx");

        var page = Assert.IsType<PlayerScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Null(Assert.Single(page.Data).Pumbility);
    }

    // The incremental-sync parameter: a tool stays current without webhooks and without re-reading
    // a player's whole history.
    [Fact]
    public async Task RecordedAfterExcludesOlderRecords()
    {
        SetupScores(
            new RecordedPhoenixScore(ApiTestData.ChartId1, PhoenixScore.From(900000), PhoenixPlate.FairGame,
                false, ApiTestData.Date1),
            new RecordedPhoenixScore(ApiTestData.ChartId1, PhoenixScore.From(978210), PhoenixPlate.MarvelousGame,
                false, ApiTestData.Date2));

        var result = await _controller.GetScores("me", "Phoenix",
            recordedAfter: ApiTestData.Date1.AddDays(1));

        var page = Assert.IsType<PlayerScorePageDto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(ApiTestData.Date2, Assert.Single(page.Data).RecordedAt);
    }

    [Fact]
    public async Task AnotherPlayersIdIs404NotForbidden()
    {
        var result = await _controller.GetScores(ApiTestData.PrivateUserId.ToString(), "Phoenix");

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task ProfileCarriesOneGameTagNotOnePerMix()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiTestData.PublicUser);
        _mediator.Setup(m => m.Send(It.Is<GetLinkedOfficialPlayerTagQuery>(q => q.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("MURLOC#8065");

        var result = await _controller.GetPlayer("me");

        var dto = Assert.IsType<PlayerV2Dto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("MURLOC#8065", dto.GameTag);
        Assert.Equal(ApiTestData.PublicUserId, dto.UserId);
    }

    // v1 returns "Anonymous" for a private player. Under v2 the caller reached them through a
    // deliberate grant, so hiding the name would make the tool useless to the person who opted in.
    [Fact]
    public async Task PrivatePlayerKeepsTheirNameAndIsFlaggedAsPrivate()
    {
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(ApiTestData.PrivateUser);
        var controller = new PlayersController(_mediator.Object, currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiTestData.PrivateUser);

        var result = await controller.GetPlayer("me");

        var dto = Assert.IsType<PlayerV2Dto>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal("HiddenPlayer", dto.Username);
        Assert.False(dto.IsPublic);
    }

    [Fact]
    public async Task JournalCarriesEveryPlayNotJustRecords()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerJournalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ScoreJournalEntry(ApiTestData.Date2, ScoreJournalEntry.OfficialImportSource,
                    ApiTestData.PublicUserId, ApiTestData.ChartId1, PhoenixScore.From(978210),
                    PhoenixPlate.MarvelousGame, false, MixEnum.Phoenix, null,
                    new JudgementCounts(1013, 4, 0, 0, 1), false)
            });

        var result = await _controller.GetJournal("me", "Phoenix");

        var page = Assert.IsType<CursorPageDto<JournalEntryDto>>(Assert.IsType<JsonResult>(result).Value);
        var row = Assert.Single(page.Data);
        Assert.False(row.IsBest);
        Assert.Equal(1013, row.Judgments!.Perfects);
        // Unbounded history: counting it would cost a second pass, so total stays null.
        Assert.Null(page.Total);
    }
}
