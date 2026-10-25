using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MassTransit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Admin;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The runs table's Finish button: it shows only on the run the handler will accept, and it
///     sends that run to be finished.
/// </summary>
public sealed class OfficialLeaderboardsAdminPageTests : ComponentTestBase
{
    private static readonly DateTimeOffset Sunday = new(2026, 10, 18, 16, 30, 5, TimeSpan.Zero);
    private readonly Mock<IBus> _bus = new();

    public OfficialLeaderboardsAdminPageTests()
    {
        Services.AddSingleton(_bus.Object);
        // User.IsAdmin is a computed Guid, so the admin is that account and needs no flag.
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(
            Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"), Name.From("DrMurloc"), true, null,
            new Uri("https://example.com/d.png"), Name.From("US")));
        Mediator.Setup(m => m.Send(It.IsAny<GetRenameProposalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RenameProposalRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMissingChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MissingChartRecord>());
    }

    private void GivenRuns(params ImportRunRecord[] runs)
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetImportRunsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
    }

    private static ImportRunRecord Failed(int id, DateTimeOffset startedAt, bool canFinish) =>
        new(id, startedAt, null, false, "Highlights", 0, 0, 0, "Execution Timeout Expired", canFinish);

    [Fact]
    public void OnlyTheRunToFinishNextOffersFinish()
    {
        GivenRuns(Failed(28, Sunday.AddDays(2), false), Failed(27, Sunday, true));

        var page = RenderComponent<OfficialLeaderboardsAdmin>();

        Assert.Single(page.FindAll("[data-testid=finish-run]"));
    }

    [Fact]
    public async Task FinishSendsThatRunToBeFinished()
    {
        GivenRuns(Failed(27, Sunday, true));
        var page = RenderComponent<OfficialLeaderboardsAdmin>();

        await page.Find("[data-testid=finish-run]").ClickAsync(new MouseEventArgs());

        _bus.Verify(b => b.Publish(
            It.Is<FinishLeaderboardRunCommand>(c => c.Mix == MixEnum.Phoenix2 && c.SnapshotId == 27),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
