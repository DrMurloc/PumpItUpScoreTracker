using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Progress;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Sessions page's first read: the header paints as soon as the player is known, the
///     patience card stands in for the hero and the history until the build returns, and a
///     private player's page goes home without painting anything of theirs.
/// </summary>
public sealed class PlayerSessionsPageTests : ComponentTestBase
{
    private static readonly Guid TargetId = Guid.NewGuid();

    public PlayerSessionsPageTests()
    {
        Services.AddSingleton(new Mock<IScoreReader>().Object);
        Services.AddScoped<SessionBreakdownBuilder>();
        Services.AddSingleton(Mock.Of<IUiNotificationHub>());
        // The loading state is a PatienceCard, which draws its phrase through the RNG seam.
        Services.AddSingleton(new Mock<IRandomNumberGenerator>().Object);
        this.RenderInteractive();
    }

    private static User Player(bool isPublic) => new(TargetId, Name.From("Reno"), isPublic, null,
        new Uri("https://piu.test/avatar.png"), null);

    private static RecentSessionsPage NoSessions() =>
        new(0, Array.Empty<RecentSessionsPage.SessionGroup>());

    private void GivenUserRead(Task<User?> read)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .Returns(read);
    }

    private void GivenRecentSessionsRead(Task<RecentSessionsPage> read)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetRecentSessionsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(read);
    }

    private IRenderedComponent<PlayerSessions> Render() =>
        RenderComponent<PlayerSessions>(p => p.Add(x => x.UserIdParam, TargetId));

    [Fact]
    public async Task TheFirstReadShowsThePatienceCardUnderTheHeader()
    {
        // Both reads are held: the player read is a real database read, so the page draws once
        // before it lands, and the build's first read is what keeps the page waiting after it.
        var user = new TaskCompletionSource<User?>();
        GivenUserRead(user.Task);
        GivenRecentSessionsRead(new TaskCompletionSource<RecentSessionsPage>().Task);

        var cut = Render();
        await cut.InvokeAsync(() => user.SetResult(Player(true)));

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("[data-testid=sessions-loading] .patience-card")));
        Assert.Contains("Reno — Sessions", cut.Find("h5").TextContent);
        Assert.Contains("Reading the session and where each score stands among peers.",
            cut.Find(".patience-sub").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=session-hero]"));
        Assert.Empty(cut.FindAll("[data-testid=session-capture-pending]"));
    }

    [Fact]
    public async Task TheCardGivesWayToTheAnswerWhenTheReadLands()
    {
        GivenUserRead(Task.FromResult<User?>(Player(true)));
        var recent = new TaskCompletionSource<RecentSessionsPage>();
        GivenRecentSessionsRead(recent.Task);

        var cut = Render();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".patience-card")));

        await cut.InvokeAsync(() => recent.SetResult(NoSessions()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sessions-empty]")));
        Assert.Empty(cut.FindAll(".patience-card"));
        Assert.Empty(cut.FindAll("[data-testid=sessions-loading]"));
    }

    [Fact]
    public void APrivatePlayerGoesHomeWithoutPaintingTheirName()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Guid.NewGuid(), Name.From("Viewer"), true, null,
            new Uri("https://piu.test/viewer.png"), null));
        GivenUserRead(Task.FromResult<User?>(Player(false)));
        GivenRecentSessionsRead(Task.FromResult(NoSessions()));

        var cut = Render();

        Assert.Contains(Services.GetRequiredService<FakeNavigationManager>().History, h => h.Uri == "/");
        Assert.DoesNotContain("Reno", cut.Markup);
        Assert.Empty(cut.FindAll(".patience-card"));
    }
}
