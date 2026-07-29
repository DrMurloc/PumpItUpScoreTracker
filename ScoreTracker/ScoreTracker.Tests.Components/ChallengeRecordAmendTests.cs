using AngleSharp.Dom;
using Bunit;
using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Commands;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.Web.Components.Challenges;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The record dialog's amend states (weekly-charts-overhaul.md §9.6). The merge rules
///     themselves are domain facts; what this pins is the dialog's two decisions — when to warn
///     that a submission replaces a higher score, and which intent that submission carries.
///     Getting the second one wrong is silent: the write is simply discarded, exactly the
///     defect the feature exists to fix.
/// </summary>
public sealed class ChallengeRecordAmendTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Chart _chart = new(Guid.NewGuid(), MixEnum.Phoenix,
        new Song("Bee", SongType.Arcade, new Uri("https://piu.test/art.png"),
            TimeSpan.FromMinutes(2), "Bang", Bpm.From(160, 160)),
        ChartType.Single, 17, MixEnum.Phoenix, null, 900, new HashSet<Skill>());

    public ChallengeRecordAmendTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.IsLoggedInAsAdmin).Returns(false);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));

        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _chart });
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());

        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(new Mock<IUserRepository>().Object);
        // The host holds four dialogs; the ones that stay closed still resolve their injections.
        Services.AddSingleton(Mock.Of<IAdminNotificationClient>());
        Services.AddSingleton(Mock.Of<IFileUploadClient>());
        Services.AddSingleton(Mock.Of<IBus>());
        Services.AddLogging();
        Services.AddScoped<ChartCatalogCache>();
        Services.AddScoped<CommunityGlowReader>();
        this.RenderInteractive();
    }

    /// <summary>
    ///     A three-row board: me at <paramref name="myScore" />, a 990k above and a 960k below.
    ///     Places are derived from the scores rather than declared — the real query ranks, so a
    ///     fixture that hand-numbers them can assert a place the data doesn't support.
    /// </summary>
    private void GivenMyEntry(int myScore, ChallengeEntrySource source)
    {
        var raw = new[]
        {
            (UserId: Guid.NewGuid(), Score: 990000, Source: ChallengeEntrySource.Manual),
            (UserId: _me, Score: myScore, Source: source),
            (UserId: Guid.NewGuid(), Score: 960000, Source: ChallengeEntrySource.Manual)
        };
        var ranked = raw.OrderByDescending(r => r.Score)
            .Select((r, i) => Row(i + 1, r.UserId, r.Score, r.Source)).ToArray();
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ranked);
    }

    private WeeklyBoardRow Row(int place, Guid userId, int score, ChallengeEntrySource source) =>
        new(place, new User(userId, "P" + place, true, null, new Uri("https://piu.test/a.png"), null),
            new WeeklyTournamentEntry(userId, _chart.Id, score, PhoenixPlate.SuperbGame, false, null, 17.0),
            source);

    /// <summary>
    ///     Inline MudDialogs render through the provider, so the fragment hosts both and every
    ///     query runs against the whole tree — the host's own output holds none of the dialog.
    /// </summary>
    private async Task<IRenderedFragment> OpenRecord()
    {
        var tree = Render(b =>
        {
            b.OpenComponent<MudDialogProvider>(0);
            b.CloseComponent();
            b.OpenComponent<ChallengeDialogHost>(1);
            b.AddComponentParameter(2, nameof(ChallengeDialogHost.Mix), MixEnum.Phoenix);
            b.CloseComponent();
        });
        var host = tree.FindComponent<ChallengeDialogHost>().Instance;
        await tree.InvokeAsync(() => host.OpenRecord(_chart.Id.ToString(), false));
        tree.WaitForAssertion(() => Assert.NotEmpty(tree.FindAll(".challenge-score-field input")));
        return tree;
    }

    private static void TypeScore(IRenderedFragment tree, string score) =>
        tree.Find(".challenge-score-field input").Input(score);

    private static IElement Action(IRenderedFragment tree, string label) =>
        tree.FindAll("button").First(b => b.TextContent.Contains(label));

    [Fact]
    public async Task TypingBelowYourBoardScoreWarnsThatItReplacesAHigherOne()
    {
        GivenMyEntry(974220, ChallengeEntrySource.Manual);
        var tree = await OpenRecord();

        TypeScore(tree, "947220");

        tree.WaitForAssertion(() =>
        {
            var panel = tree.Find(".challenge-amend");
            Assert.Contains("This replaces a higher score", panel.TextContent);
            // The cost, not just the fact: the score it displaces and the place you fall to.
            Assert.Contains("974,220", panel.TextContent);
            Assert.Contains("#2 → #3 of 3", panel.TextContent);
        });
    }

    [Fact]
    public async Task TypingAboveYourBoardScoreShowsNoWarning()
    {
        GivenMyEntry(947220, ChallengeEntrySource.Manual);
        var tree = await OpenRecord();

        TypeScore(tree, "974220");

        tree.WaitForAssertion(() => Assert.Contains("New best", tree.Markup));
        Assert.Empty(tree.FindAll(".challenge-amend"));
        Assert.NotNull(Action(tree, "Submit"));
    }

    [Fact]
    public async Task ALowerSubmissionCarriesTheReplaceIntent()
    {
        GivenMyEntry(974220, ChallengeEntrySource.Manual);
        var tree = await OpenRecord();
        TypeScore(tree, "947220");
        tree.WaitForAssertion(() => Assert.NotEmpty(tree.FindAll(".challenge-amend")));

        Action(tree, "Replace with lower score").Click();

        _mediator.Verify(m => m.Send(
            It.Is<RegisterWeeklyChartScoreCommand>(c => c.Intent == WeeklyEntryIntent.Replace
                                                        && c.Entry.Score == (PhoenixScore)947220
                                                        && c.Source == ChallengeEntrySource.Manual),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARaisingSubmissionKeepsTheBestWinsIntent()
    {
        // The importer's rule is the default, and a raise has no reason to leave it — sending
        // Replace here would work today and quietly matter the day the two rules diverge.
        GivenMyEntry(947220, ChallengeEntrySource.Manual);
        var tree = await OpenRecord();
        TypeScore(tree, "974220");
        tree.WaitForAssertion(() => Assert.Contains("New best", tree.Markup));

        Action(tree, "Submit").Click();

        _mediator.Verify(m => m.Send(
            It.Is<RegisterWeeklyChartScoreCommand>(c => c.Intent == WeeklyEntryIntent.BestWins
                                                        && c.Entry.Score == (PhoenixScore)974220),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnImportedEntryIsReadOnlyAndHasNoSubmitAction()
    {
        GivenMyEntry(981540, ChallengeEntrySource.Official);
        var tree = await OpenRecord();

        var panel = tree.Find(".challenge-amend.locked");
        Assert.Contains("Recorded from your PIUGAME account", panel.TextContent);
        Assert.True(tree.Find(".challenge-score-field input").HasAttribute("readonly"));
        Assert.DoesNotContain(tree.FindAll("button"), b => b.TextContent.Contains("Submit"));
        Assert.NotNull(Action(tree, "Close"));
    }
}
