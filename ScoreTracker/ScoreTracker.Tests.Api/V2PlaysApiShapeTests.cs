using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;
using Xunit;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     Pins <c>POST api/v2/players/me/plays</c>, the one write on v2 (docs/design/rise.md §6.3):
///     its 201 body, and the refusals a tool has to be able to act on — the judgment checksum
///     above all, since it is what keeps a misread screen out of a record.
/// </summary>
public sealed class V2PlaysApiShapeTests
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public V2PlaysApiShapeTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApiTestData.Chart1, ApiTestData.Chart2 });
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

    // A thousand Perfects with the combo intact: the formula lands on exactly 1,000,000.
    private static ObservedPlayDto PerfectPlay(Guid? chartId = null, string? song = null, string? type = null,
        int? level = null, string? award = "PG", int score = 1_000_000)
    {
        return new ObservedPlayDto(chartId, song, type, level, Perfects: 1000, Greats: 0, Goods: 0, Bads: 0,
            Misses: 0, MaxCombo: 1000, Score: score, IsBroken: false, Award: award, PlayedAt: ApiTestData.Date1);
    }

    private static RecordPlaysRequestDto Request(params ObservedPlayDto[] plays)
    {
        return new RecordPlaysRequestDto("Rise", "capture", plays);
    }

    private static string ProblemType(IActionResult result)
    {
        return ((ProblemDetails)((ObjectResult)result).Value!).Type!;
    }

    [Fact]
    public async Task ARecordedPlayAnswersWithTheCountAndTheMix()
    {
        var result = await Controller().RecordPlays(Request(PerfectPlay(ApiTestData.ChartId1)));

        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)result).StatusCode);
        JsonApproval.AssertWireShape("""
            {
              "recorded": 1,
              "mix": "Rise",
              "scoringModel": "phoenix"
            }
            """, result);
    }

    [Fact]
    public async Task ThePlayGoesToTheLedgerDatedByThePlayWithItsJudgments()
    {
        await Controller().RecordPlays(Request(PerfectPlay(ApiTestData.ChartId1)));

        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.UserId == ApiTestData.PublicUserId && c.Mix == MixEnum.Rise && c.Source == "api:capture" &&
                !c.RecordBrokenAsBest && c.Plays.Count == 1 && c.Plays[0].ChartId == ApiTestData.ChartId1 &&
                !c.Plays[0].IsBroken && (int)c.Plays[0].Score!.Value == 1_000_000 &&
                c.Plays[0].Plate == PhoenixPlate.PerfectGame && c.Plays[0].PlayedAt == ApiTestData.Date1 &&
                c.Plays[0].Judgements!.Perfects == 1000 && c.Plays[0].Judgements!.MaxCombo == 1000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AScoreTheJudgmentsDoNotProduceRefusesTheWholeRequest()
    {
        // 900 Perfects and 100 Misses with the combo at 900 is a 900,000, not a million.
        var misread = new ObservedPlayDto(ApiTestData.ChartId1, null, null, null, Perfects: 900, Greats: 0,
            Goods: 0, Bads: 0, Misses: 100, MaxCombo: 900, Score: 1_000_000, IsBroken: false, Award: null,
            PlayedAt: ApiTestData.Date1);

        var result = await Controller().RecordPlays(Request(PerfectPlay(ApiTestData.ChartId2), misread));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith("/judgments-do-not-reconcile", ProblemType(result));
        _mediator.Verify(m => m.Send(It.IsAny<RecordSittingPlaysCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AScoreOnePointOffStillReconciles()
    {
        // The formula is exact to ±1, so a screen read one point off is the machine's rounding, not a misread.
        var result = await Controller().RecordPlays(Request(PerfectPlay(ApiTestData.ChartId1, score: 999_999)));

        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)result).StatusCode);
    }

    [Fact]
    public async Task AChartCanBeNamedBySongTypeAndLevel()
    {
        var result = await Controller().RecordPlays(Request(PerfectPlay(song: "conflict", type: "single", level: 20)));

        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)result).StatusCode);
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c => c.Plays[0].ChartId == ApiTestData.ChartId1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AChartTheMixDoesNotHaveIs404()
    {
        var result = await Controller().RecordPlays(Request(PerfectPlay(song: "Conflict", type: "Single", level: 99)));

        Assert.Equal(StatusCodes.Status404NotFound, ((ObjectResult)result).StatusCode);
    }

    // 990 Perfects and 10 Greats with the combo intact: nothing below Great, so a Full Combo on
    // Rise, and (.995 × 996 + .005 × 1000) / 1000 = 996,020.
    private static ObservedPlayDto FullComboPlay(string? award)
    {
        return new ObservedPlayDto(ApiTestData.ChartId1, null, null, null, Perfects: 990, Greats: 10, Goods: 0,
            Bads: 0, Misses: 0, MaxCombo: 1000, Score: 996_020, IsBroken: false, Award: award,
            PlayedAt: ApiTestData.Date1);
    }

    [Fact]
    public async Task AnAwardTheMixDoesNotHandOutIsRefused()
    {
        var refused = await Controller().RecordPlays(Request(PerfectPlay(ApiTestData.ChartId1, award: "TG")));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)refused).StatusCode);
        Assert.EndsWith("/award-invalid", ProblemType(refused));
    }

    [Fact]
    public async Task TheAwardIsDerivedFromTheJudgmentsAndAClaimOnlyHasToAgree()
    {
        // Omitted: derived. A Full Combo run on Rise stores the plate the mark coincides with.
        await Controller().RecordPlays(Request(FullComboPlay(award: null)));
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c => c.Plays[0].Plate == PhoenixPlate.UltimateGame),
            It.IsAny<CancellationToken>()), Times.Once);

        // Claimed in the mix's own shorthand and earned: fine.
        var agreed = await Controller().RecordPlays(Request(FullComboPlay(award: "fc")));
        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)agreed).StatusCode);

        // Claimed and not earned — ten Greats are not a Perfect Game: refused, nothing written.
        var overclaimed = await Controller().RecordPlays(Request(FullComboPlay(award: "PG")));
        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)overclaimed).StatusCode);
        Assert.EndsWith("/award-does-not-reconcile", ProblemType(overclaimed));
        _mediator.Verify(m => m.Send(It.IsAny<RecordSittingPlaysCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ABreakCarriesNoAwardAndTheToolsSeatingChoiceTravelsWithIt()
    {
        var broken = PerfectPlay(ApiTestData.ChartId1) with { IsBroken = true, Award = "PG" };

        // Rise's default is Phoenix's — a break is history, never a best.
        await Controller().RecordPlays(Request(broken));
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                !c.RecordBrokenAsBest && c.Plays.Count == 1 && c.Plays[0].IsBroken && c.Plays[0].Plate == null),
            It.IsAny<CancellationToken>()), Times.Once);

        // The tool's own choice outranks the default, and a break still carries no award.
        await Controller().RecordPlays(new RecordPlaysRequestDto("Rise", "capture", new[] { broken },
            RecordBrokenAsBest: true));
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.RecordBrokenAsBest && c.Plays[0].IsBroken && c.Plays[0].Plate == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EveryPlayOfARequestTravelsInOneCommand()
    {
        await Controller().RecordPlays(Request(PerfectPlay(ApiTestData.ChartId1), PerfectPlay(ApiTestData.ChartId2)));

        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.Plays.Select(p => p.ChartId).SequenceEqual(new[] { ApiTestData.ChartId1, ApiTestData.ChartId2 })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APlayTimeIsRequiredAndCannotBeInTheFuture()
    {
        var omitted = PerfectPlay(ApiTestData.ChartId1) with { PlayedAt = default };
        var future = PerfectPlay(ApiTestData.ChartId1) with { PlayedAt = ApiTestClock.Now.AddDays(1) };
        var justNow = PerfectPlay(ApiTestData.ChartId1) with { PlayedAt = ApiTestClock.Now.AddMinutes(1) };

        Assert.EndsWith("/played-at-invalid", ProblemType(await Controller().RecordPlays(Request(omitted))));
        Assert.EndsWith("/played-at-invalid", ProblemType(await Controller().RecordPlays(Request(future))));
        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)await Controller().RecordPlays(Request(justNow))).StatusCode);
    }

    [Fact]
    public async Task AToolCredentialCannotRecordPlays()
    {
        var result = await Controller(ToolId).RecordPlays(Request(PerfectPlay(ApiTestData.ChartId1)));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith("/tool-has-no-self", ProblemType(result));
    }

    [Theory]
    [InlineData("XX", "/legacy-mix")]
    [InlineData("Prime2", "/legacy-mix")]
    [InlineData("Phoenix 2", "/mix-required")]
    [InlineData(null, "/mix-required")]
    public async Task OnlyAPhoenixScoredMixTakesPlays(string? mix, string problem)
    {
        var result = await Controller().RecordPlays(new RecordPlaysRequestDto(mix, "capture",
            new[] { PerfectPlay(ApiTestData.ChartId1) }));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith(problem, ProblemType(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("screen grab")]
    [InlineData("a-name-that-runs-past-the-thirty-two-limit")]
    // "api:" plus a 29-character name overflows the 32-character source column.
    [InlineData("abcdefghijklmnopqrstuvwxyz123")]
    public async Task TheSourceNamesTheToolInOneToken(string? source)
    {
        var result = await Controller().RecordPlays(new RecordPlaysRequestDto("Rise", source,
            new[] { PerfectPlay(ApiTestData.ChartId1) }));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith("/source-required", ProblemType(result));
    }

    [Fact]
    public async Task AnEmptyOrOversizedBatchIsRefused()
    {
        var empty = await Controller().RecordPlays(Request());
        Assert.EndsWith("/plays-required", ProblemType(empty));

        var tooMany = Enumerable.Repeat(PerfectPlay(ApiTestData.ChartId1), PlayersController.MaxPlaysPerRequest + 1)
            .ToArray();
        var oversized = await Controller().RecordPlays(Request(tooMany));
        Assert.EndsWith("/plays-required", ProblemType(oversized));
    }

    // A best read off a song list: the score, and nothing that proves it.
    private static ObservedPlayDto ScoreOnly(int score, string? award = null, bool isBroken = false)
    {
        return new ObservedPlayDto(ApiTestData.ChartId1, null, null, null, Perfects: null, Greats: null, Goods: null,
            Bads: null, Misses: null, MaxCombo: null, Score: score, IsBroken: isBroken, Award: award,
            PlayedAt: ApiTestData.Date1);
    }

    [Fact]
    public async Task APlayCanArriveWithoutItsJudgments()
    {
        // The body as a tool that read only the score sends it: no counts, no max combo, no award.
        var body = JsonSerializer.Deserialize<RecordPlaysRequestDto>("""
            {
              "mix": "Rise",
              "source": "capture",
              "plays": [
                {
                  "chartId": "11111111-1111-1111-1111-111111111111",
                  "score": 990000,
                  "isBroken": false,
                  "playedAt": "2026-01-15T00:00:00+00:00"
                }
              ]
            }
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var result = await Controller().RecordPlays(body);

        JsonApproval.AssertWireShape("""
            {
              "recorded": 1,
              "mix": "Rise",
              "scoringModel": "phoenix"
            }
            """, result);
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.Plays.Count == 1 && c.Plays[0].ChartId == ApiTestData.ChartId1 &&
                (int)c.Plays[0].Score!.Value == 990_000 && c.Plays[0].Judgements == null &&
                c.Plays[0].Plate == null && c.Plays[0].PlayedAt == ApiTestData.Date1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1000, null, null, null, null, null)]
    [InlineData(1000, 0, 0, 0, 0, null)]
    [InlineData(null, null, null, null, null, 1000)]
    public async Task TheJudgmentsComeAllSixOrNone(int? perfects, int? greats, int? goods, int? bads, int? misses,
        int? maxCombo)
    {
        var partial = PerfectPlay(ApiTestData.ChartId1) with
        {
            Perfects = perfects, Greats = greats, Goods = goods, Bads = bads, Misses = misses, MaxCombo = maxCombo
        };

        var result = await Controller().RecordPlays(Request(partial));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith("/judgments-incomplete", ProblemType(result));
        _mediator.Verify(m => m.Send(It.IsAny<RecordSittingPlaysCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ZeroesAreJudgmentsNotTheirAbsence()
    {
        var zeroes = PerfectPlay(ApiTestData.ChartId1) with
        {
            Perfects = 0, Greats = 0, Goods = 0, Bads = 0, Misses = 0, MaxCombo = 0
        };

        var result = await Controller().RecordPlays(Request(zeroes));

        Assert.EndsWith("/judgments-do-not-reconcile", ProblemType(result));
    }

    [Fact]
    public async Task JudgmentsTheFormulaCannotScoreDoNotReconcile()
    {
        // Ten thousand notes is past any chart. The formula scores such a screen as zero, and a zero
        // claimed alongside it must not pass as a zero-point Perfect Game.
        var unscorable = PerfectPlay(ApiTestData.ChartId1, award: null, score: 0) with
        {
            Perfects = 10_000, MaxCombo = 10_000
        };

        var result = await Controller().RecordPlays(Request(unscorable));

        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        Assert.EndsWith("/judgments-do-not-reconcile", ProblemType(result));
        _mediator.Verify(m => m.Send(It.IsAny<RecordSittingPlaysCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WithoutJudgmentsTheAwardIsTheOneSent()
    {
        // Nothing checks a Full Combo below a million without the counts, so the claim stands.
        await Controller().RecordPlays(Request(ScoreOnly(996_020, award: "FC")));
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.Plays[0].Plate == PhoenixPlate.UltimateGame && c.Plays[0].Judgements == null),
            It.IsAny<CancellationToken>()), Times.Once);

        // And none was sent, so none is recorded.
        await Controller().RecordPlays(Request(ScoreOnly(996_020)));
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.Plays[0].Plate == null && c.Plays[0].Judgements == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithoutJudgmentsOnlyAMillionIsAPerfectGame()
    {
        // A million is a Perfect Game whether or not it says so.
        await Controller().RecordPlays(Request(ScoreOnly(1_000_000)));
        var claimed = await Controller().RecordPlays(Request(ScoreOnly(1_000_000, award: "pg")));
        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)claimed).StatusCode);
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.Plays[0].Plate == PhoenixPlate.PerfectGame),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        // The score disagreeing with the award is refused, the same as judgments disagreeing with it.
        var otherAward = await Controller().RecordPlays(Request(ScoreOnly(1_000_000, award: "FC")));
        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)otherAward).StatusCode);
        Assert.EndsWith("/award-does-not-reconcile", ProblemType(otherAward));
        var perfectBelow = await Controller().RecordPlays(Request(ScoreOnly(999_990, award: "PG")));
        Assert.EndsWith("/award-does-not-reconcile", ProblemType(perfectBelow));
        _mediator.Verify(m => m.Send(It.IsAny<RecordSittingPlaysCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WithoutJudgmentsAPassScoresAboveZero()
    {
        // A score left out of the body reads as zero, and with no judgments nothing else would notice.
        var pass = await Controller().RecordPlays(Request(ScoreOnly(0)));
        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)pass).StatusCode);
        Assert.EndsWith("/score-invalid", ProblemType(pass));
        _mediator.Verify(m => m.Send(It.IsAny<RecordSittingPlaysCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // A break can score zero; the ledger decides whether anything was hit.
        var broken = await Controller().RecordPlays(Request(ScoreOnly(0, isBroken: true)));
        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)broken).StatusCode);
    }

    [Fact]
    public async Task OneRequestCanMixJudgedAndUnjudgedPlays()
    {
        var result = await Controller().RecordPlays(Request(PerfectPlay(ApiTestData.ChartId2), ScoreOnly(990_000)));

        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)result).StatusCode);
        _mediator.Verify(m => m.Send(It.Is<RecordSittingPlaysCommand>(c =>
                c.Plays.Count == 2 &&
                c.Plays[0].ChartId == ApiTestData.ChartId2 && c.Plays[0].Judgements!.Perfects == 1000 &&
                c.Plays[1].ChartId == ApiTestData.ChartId1 && c.Plays[1].Judgements == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
