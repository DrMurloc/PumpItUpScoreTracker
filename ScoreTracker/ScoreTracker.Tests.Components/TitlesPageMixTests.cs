using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Progress;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Titles are per-mix and a title's name is its key, so this page cannot fall back to
///     another mix's ladder the way the Pumbility and Official Leaderboards frames fall back
///     to Phoenix. It says which of two things is true instead, because they are different
///     promises: Prime 2 awarded titles and has no ladder here yet, while everything older
///     never awarded one at all.
/// </summary>
public sealed class TitlesPageMixTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _settings = new();

    public TitlesPageMixTests()
    {
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_settings.Object);
    }

    private IRenderedComponent<Titles> RenderAt(MixEnum mix)
    {
        _settings.Setup(s => s.GetSelectedMix()).ReturnsAsync(mix);
        return RenderComponent<Titles>();
    }

    [Fact]
    public void APrePrime2MixSaysTitlesNeverExistedThere()
    {
        var cut = RenderAt(MixEnum.Prex3);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Pump It Up didn't award titles until Prime 2.", cut.Markup);
            // A fact, not a promise: nothing here should read as "come back later".
            Assert.DoesNotContain("yet.", cut.Markup);
        });
    }

    [Fact]
    public void Prime2SaysTheLadderIsNotBuiltYet()
    {
        var cut = RenderAt(MixEnum.Prime2);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("isn't available for Prime 2 yet.", cut.Markup);
            Assert.DoesNotContain("didn't award titles", cut.Markup);
        });
    }

    /// <summary>
    ///     And a mix with no ladder never asks the database for one — the rarity and holder
    ///     reads would each round-trip to come back empty.
    /// </summary>
    [Fact]
    public void AMixWithNoLadderIssuesNoTitleQueries()
    {
        RenderAt(MixEnum.Prime);

        _mediator.Verify(m => m.Send(It.IsAny<GetTitleProgressQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<GetTitleRarityQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>XX has a real ladder and must keep loading it — the branch cuts both ways.</summary>
    [Fact]
    public void XXStillLoadsItsLadder()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetTitleProgressQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TitleProgress>().AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetTitleRarityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleRarityRecord(new Dictionary<Name, int>(), 0));

        var cut = RenderAt(MixEnum.XX);

        cut.WaitForAssertion(() => Assert.DoesNotContain("didn't award titles", cut.Markup));
        _mediator.Verify(m => m.Send(It.IsAny<GetTitleProgressQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
