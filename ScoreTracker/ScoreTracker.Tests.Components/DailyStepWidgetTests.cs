using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Daily Step widget, 1x1: the top three and your standing. Players on the same score share a
///     place, so a tie for first reads #1 twice in the top color and the next player reads #3.
/// </summary>
public sealed class DailyStepWidgetTests : ComponentTestBase
{
    private readonly Guid _me = Guid.NewGuid();

    private readonly Chart _chart = new(Guid.NewGuid(), MixEnum.Phoenix,
        new Song("District 1", SongType.Arcade, new Uri("https://piu.test/art.png"),
            TimeSpan.FromMinutes(2), "Bang", Bpm.From(160, 160)),
        ChartType.Single, 20, MixEnum.Phoenix, null, 900);

    public DailyStepWidgetTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));

        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalSubject>());
        Mediator.Setup(m => m.Send(It.IsAny<GetDailyStepQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyStepBoard(_chart.Id, DateTimeOffset.MinValue, false, DateTimeOffset.MaxValue));
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _chart });

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids.Select(id =>
                new User(id, Name.From("P" + id.ToString("N")[..6]), true, null,
                    new Uri("https://piu.test/avatar.png"), null)).ToArray());
        Services.AddSingleton(users.Object);
        Services.AddScoped<ChartCatalogCache>();
        Services.AddScoped<CommunityGlowReader>();
        this.RenderInteractive();
    }

    private DailyStepEntry Entry(Guid userId, int score, PhoenixPlate plate = PhoenixPlate.SuperbGame) =>
        new(userId, _chart.Id, score, plate, false, 20.0, ChallengeEntrySource.Official);

    private IRenderedComponent<DailyStepWidget> Render(params DailyStepEntry[] entries)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetDailyStepEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "daily-step", null, 0, "1x1",
            WidgetConfigJson.Write(new DailyStepConfig()), 1);
        return base.Render(builder =>
        {
            builder.OpenComponent<DailyStepWidget>(0);
            builder.AddAttribute(1, nameof(DailyStepWidget.Widget), widget);
            builder.AddAttribute(2, nameof(DailyStepWidget.EffectiveMix), MixEnum.Phoenix);
            builder.CloseComponent();
        }).FindComponent<DailyStepWidget>();
    }

    [Fact]
    public void ATieForFirstReadsFirstTwiceInTheTopColor()
    {
        var cut = Render(
            Entry(Guid.NewGuid(), 1_000_000, PhoenixPlate.PerfectGame),
            Entry(_me, 1_000_000, PhoenixPlate.PerfectGame),
            Entry(Guid.NewGuid(), 990_000));

        cut.WaitForAssertion(() =>
        {
            var places = cut.FindAll(".dash-top3-place");
            Assert.Equal(new[] { "1", "1", "3" }, places.Select(p => p.TextContent.Trim()).ToArray());
            Assert.Contains("--rarity-prism", places[0].GetAttribute("style"));
            Assert.Contains("--rarity-prism", places[1].GetAttribute("style"));
            Assert.Equal("#1", cut.Find(".dash-mine-rank").TextContent.Trim());
        });
    }

    [Fact]
    public void ASoleEntrantIsFirstInTheTopColor()
    {
        var cut = Render(Entry(_me, 950_000));

        cut.WaitForAssertion(() =>
        {
            var first = cut.FindAll(".dash-top3-place")[0];
            Assert.Equal("1", first.TextContent.Trim());
            Assert.Contains("--rarity-prism", first.GetAttribute("style"));
        });
    }
}
