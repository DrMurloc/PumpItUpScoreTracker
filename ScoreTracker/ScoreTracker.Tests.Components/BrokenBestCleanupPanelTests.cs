using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.Account;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Your Data cleanup card (docs/design/delete-my-data.md §10). What it removes is
///     re-derivable, so it sits above the arm gate and leans on the count as its guard.
/// </summary>
public sealed class BrokenBestCleanupPanelTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private static readonly Guid UserId = Guid.NewGuid();

    public BrokenBestCleanupPanelTests()
    {
        Services.AddSingleton(_mediator.Object);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(UserId, "Tester", true, null, null, null));
        Counts((MixEnum.Phoenix, 0), (MixEnum.Phoenix2, 0));
    }

    private void Counts(params (MixEnum Mix, int Count)[] counts)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetBrokenRecordCountsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BrokenRecordCount>)counts
                .Select(c => new BrokenRecordCount(c.Mix, c.Count)).ToArray());
    }

    [Fact]
    public void TheButtonNamesHowManyRecordsAreAboutToGo()
    {
        // The count IS the guard here, the same way the scoped delete's blast-radius label is.
        Counts((MixEnum.Phoenix, 3), (MixEnum.Phoenix2, 268));

        var cut = RenderComponent<BrokenBestCleanupPanel>();

        Assert.Contains("Remove 271 broken records", cut.Markup);
    }

    [Fact]
    public void EveryPhoenixScoringMixIsListedEvenAtZero()
    {
        // Rendering only what it found would be indistinguishable from forgetting to look.
        Counts((MixEnum.Phoenix, 0), (MixEnum.Phoenix2, 268));

        var cut = RenderComponent<BrokenBestCleanupPanel>();

        Assert.Contains(MixEnum.Phoenix.GetName(), cut.Markup);
        Assert.Contains(MixEnum.Phoenix2.GetName(), cut.Markup);
    }

    [Fact]
    public void WithNothingBrokenTheCardSaysSoAndCannotBePressed()
    {
        var cut = RenderComponent<BrokenBestCleanupPanel>();

        Assert.Contains("Broken Best Cleanup Empty", cut.Markup);
        // No promise of reversibility either — there is nothing to reverse.
        Assert.DoesNotContain("Broken Best Cleanup Reversible", cut.Markup);
        Assert.All(cut.FindAll("button"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void OneMixWithBrokenRecordsGetsNoRedundantPerMixShortcut()
    {
        Counts((MixEnum.Phoenix, 0), (MixEnum.Phoenix2, 268));

        var cut = RenderComponent<BrokenBestCleanupPanel>();

        // "Phoenix 2 only" would do exactly what the one button already does.
        Assert.DoesNotContain("only", cut.Markup);
    }

    [Fact]
    public async Task CleaningUpAsksForEveryMixThatHasSomething()
    {
        Counts((MixEnum.Phoenix, 3), (MixEnum.Phoenix2, 268));
        _mediator.Setup(m => m.Send(It.IsAny<DeleteBrokenRecordsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(271);

        var cut = RenderComponent<BrokenBestCleanupPanel>();
        await cut.FindAll("button").First(b => b.TextContent.Contains("Remove 271"))
            .ClickAsync(new MouseEventArgs());

        _mediator.Verify(m => m.Send(It.Is<DeleteBrokenRecordsCommand>(c =>
                c.UserId == UserId && c.Mixes.Count == 2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ThePerMixShortcutScopesToThatMixAlone()
    {
        Counts((MixEnum.Phoenix, 3), (MixEnum.Phoenix2, 268));
        _mediator.Setup(m => m.Send(It.IsAny<DeleteBrokenRecordsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(268);

        var cut = RenderComponent<BrokenBestCleanupPanel>();
        await cut.FindAll("button").First(b => b.TextContent.Contains($"{MixEnum.Phoenix2.GetName()} only"))
            .ClickAsync(new MouseEventArgs());

        _mediator.Verify(m => m.Send(It.Is<DeleteBrokenRecordsCommand>(c =>
                c.Mixes.Single() == MixEnum.Phoenix2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TheCardRereadsItsCountsAfterCleaningUp()
    {
        // Leaving the old number standing would invite a second press against rows already gone.
        Counts((MixEnum.Phoenix, 0), (MixEnum.Phoenix2, 268));
        _mediator.Setup(m => m.Send(It.IsAny<DeleteBrokenRecordsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(268)
            .Callback(() => Counts((MixEnum.Phoenix, 0), (MixEnum.Phoenix2, 0)));

        var cut = RenderComponent<BrokenBestCleanupPanel>();
        await cut.FindAll("button").First(b => b.TextContent.Contains("Remove 268"))
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Contains("Broken Best Cleanup Empty", cut.Markup));
    }
}
