using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MassTransit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Seasons.Contracts.Messages;
using ScoreTracker.Seasons.Contracts.Queries;
using ScoreTracker.SharedKernel.ValueTypes;
// 'Seasons' is a vertical's namespace as well as the page's name, so the page is aliased.
using SeasonsPage = ScoreTracker.Web.Pages.Admin.Seasons;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The season console (docs/design/seasons.md §12.3): it lists the calendar and its two buttons
///     publish the same messages the schedule does — the whole point of the page is that a hand-run
///     catch-up cannot drift from the nightly path.
/// </summary>
public sealed class AdminSeasonsPageTests : ComponentTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IBus> _bus = new();

    public AdminSeasonsPageTests()
    {
        var clock = new Mock<IDateTimeOffsetAccessor>();
        clock.SetupGet(c => c.Now).Returns(Now);
        Services.AddSingleton(clock.Object);
        Services.AddSingleton(_bus.Object);
        // User.IsAdmin is a computed Guid, so the admin is that account and needs no flag.
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(
            Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"), Name.From("DrMurloc"), true, null,
            new Uri("https://example.com/d.png"), Name.From("US")));
    }

    private void GivenSeasons(params SeasonRecord[] seasons)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetSeasonsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seasons);
    }

    private static SeasonRecord Season(int year, int quarter, DateTimeOffset? sealedAt = null)
    {
        var offset = TimeSpan.FromHours(-5);
        var month = quarter * 3;
        return new SeasonRecord(SeasonId.From(year, quarter), $"Q{quarter} {year}",
            new DateTimeOffset(new DateTime(year, month - 2, 1, 0, 0, 0), offset),
            new DateTimeOffset(new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59), offset),
            sealedAt, false);
    }

    [Fact]
    public void EachSeasonSaysWhetherItIsRunningEndedOrSealed()
    {
        GivenSeasons(Season(2026, 4), Season(2026, 3, sealedAt: Now.AddDays(-20)), Season(2026, 2));

        var page = RenderComponent<SeasonsPage>();
        var rows = page.Find("[data-testid=seasons-table]").QuerySelectorAll("tbody tr");

        Assert.Equal(3, rows.Length);
        Assert.Contains("Running", rows[0].TextContent);
        Assert.Contains("Sealed", rows[1].TextContent);
        Assert.Contains("Ended, not yet sealed", rows[2].TextContent);
    }

    [Fact]
    public void AnEmptyCalendarExplainsWhatTheTwoButtonsDo()
    {
        GivenSeasons();

        var page = RenderComponent<SeasonsPage>();

        Assert.NotNull(page.Find("[data-testid=seasons-none]"));
        Assert.Empty(page.FindAll("[data-testid=seasons-table]"));
    }

    [Fact]
    public async Task RollNowPublishesTheSameMessageTheScheduleDoes()
    {
        GivenSeasons(Season(2026, 4));
        var page = RenderComponent<SeasonsPage>();

        await page.Find("[data-testid=seasons-roll]").ClickAsync(new MouseEventArgs());

        _bus.Verify(b => b.Publish(It.IsAny<RollSeasonCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(page.Find("[data-testid=seasons-message]"));
    }

    [Fact]
    public async Task BackfillPublishesItsOwnCommandAndSaysTheWorkIsInTheBackground()
    {
        GivenSeasons(Season(2026, 4));
        var page = RenderComponent<SeasonsPage>();

        await page.Find("[data-testid=seasons-backfill]").ClickAsync(new MouseEventArgs());

        _bus.Verify(b => b.Publish(It.IsAny<BackfillSeasonsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("background", page.Find("[data-testid=seasons-message]").TextContent);
    }
}
