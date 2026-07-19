using System;
using System.Collections.Generic;
using System.Threading;
using Bunit;
using Bunit.TestDoubles;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Communities;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The community player page: official tiles render only when the account resolves to a
///     linked mirror player, and the folder compare renders only for a logged-in viewer
///     looking at someone else.
/// </summary>
public sealed class CommunityPlayerPageTests : ComponentTestBase
{
    private static readonly Guid TargetId = Guid.NewGuid();
    private readonly Mock<IMediator> _mediator = new();

    public CommunityPlayerPageTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityPlayerProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunityPlayerProfileRecord(TargetId, Name.From("Reno"),
                new Uri("https://piu.test/avatar.png"), Name.From("United States"), true,
                942, 14208, 951, 928, 23.4, 26, 812,
                new[] { new CommunityFolderCompletionRecord(20, 49, 50) }));
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPlayerStandingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfficialPlayerStandingRecord?)null);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d => d.Now == new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero)));
    }

    private void GivenViewer(Guid id)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(id, Name.From("Viewer"), true, null,
            new Uri("https://piu.test/viewer.png"), null));
    }

    private IRenderedComponent<CommunityPlayer> Render()
    {
        Services.GetRequiredService<FakeNavigationManager>()
            .NavigateTo($"/Community/Player?CommunityName=Acme&UserId={TargetId}");
        return RenderComponent<CommunityPlayer>();
    }

    [Fact]
    public void UnlinkedPlayerShowsNoOfficialTiles()
    {
        GivenViewer(Guid.NewGuid());
        var cut = Render();
        Assert.Contains("Reno", cut.Markup);
        Assert.Contains("942", cut.Markup);
        Assert.DoesNotContain("Official #", cut.Markup);
        Assert.DoesNotContain("top-board charts", cut.Markup);
    }

    [Fact]
    public void LinkedPlayerShowsOfficialStandingAndProfileLink()
    {
        GivenViewer(Guid.NewGuid());
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPlayerStandingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialPlayerStandingRecord("RENO", 88, 61));
        var cut = Render();
        Assert.Contains("Official #88", cut.Markup);
        Assert.Contains("61 top-board charts", cut.Markup);
        Assert.Contains("/OfficialLeaderboards/Players?player=RENO", cut.Markup);
    }

    [Fact]
    public void CompareRendersForAnotherPlayerButNotYourself()
    {
        GivenViewer(Guid.NewGuid());
        Assert.Contains("Compare in folder", Render().Markup);

        GivenViewer(TargetId);
        Assert.DoesNotContain("Compare in folder", Render().Markup);
    }
}
