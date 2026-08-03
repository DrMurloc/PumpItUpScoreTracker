using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Score check panel on /UploadPhoenixScores. What matters at this level: it names what
///     it found, it only offers a repair when there is something to repair, the deep scan is
///     gated on the month's allowance, and every action is blocked while no credential is
///     available or an import is already running.
/// </summary>
public sealed class ScoreCheckPanelTests : ComponentTestBase
{
    private static readonly DateTimeOffset RanAt = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();

    public ScoreCheckPanelTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton<IUiNotificationHub>(new UiNotificationHub());
        Services.AddSingleton(Mock.Of<ISnackbar>());
    }

    private void Answer(ImportCheckReport? report, int deepScansLeft = 3)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetLastImportCheckQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LastImportCheck(report, deepScansLeft,
                new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    private IRenderedComponent<ScoreCheckPanel> Render(bool credentials = true, bool busy = false)
    {
        return RenderComponent<ScoreCheckPanel>(p => p
            .Add(c => c.Mix, MixEnum.Phoenix)
            .Add(c => c.CardId, "card")
            .Add(c => c.GameTag, "TAG #1")
            .Add(c => c.Busy, busy)
            .Add(c => c.Credentials, () => credentials
                ? new TypedCredentialSource("user", "pass")
                : null));
    }

    private static ImportCheckReport Report(ImportCheckVerdict verdict,
        params ImportCheckDifference[] differences)
    {
        return new ImportCheckReport(MixEnum.Phoenix, RanAt, verdict, 64466, 63420, 2851, 2848, differences);
    }

    [Fact]
    public void AnInSyncAccountSaysSoAndOffersNoRepair()
    {
        Answer(Report(ImportCheckVerdict.InSync));

        var markup = Render().Markup;

        Assert.Contains("In sync", markup);
        Assert.DoesNotContain("Add these scores", markup);
    }

    [Fact]
    public void MissingScoresAreCountedAndTheirLevelsNamed()
    {
        Answer(Report(ImportCheckVerdict.MissingScores,
            new ImportCheckDifference("18", 18, ImportCheckDifferenceKind.Missing, 1),
            new ImportCheckDifference("21", 21, ImportCheckDifferenceKind.Missing, 2)));

        var markup = Render().Markup;

        Assert.Contains("PIUGAME has 3 scores that aren't here.", markup);
        Assert.Contains("Level 18", markup);
        Assert.Contains("Level 21", markup);
        Assert.Contains("Add these scores", markup);
    }

    [Fact]
    public void BucketsThatAreNotASingleLevelAreNamedAsThemselves()
    {
        Answer(Report(ImportCheckVerdict.MissingScores,
            new ImportCheckDifference("coop", null, ImportCheckDifferenceKind.Missing, 1),
            new ImportCheckDifference("27over", null, ImportCheckDifferenceKind.Missing, 1),
            new ImportCheckDifference("sub10", null, ImportCheckDifferenceKind.Missing, 1)));

        var markup = Render().Markup;

        Assert.Contains("CO-OP", markup);
        Assert.Contains("Level 27 and above", markup);
        Assert.Contains("Below level 10", markup);
    }

    [Fact]
    public void HoldingMoreThanPiuGameIsNeverOfferedAsSomethingToFix()
    {
        Answer(Report(ImportCheckVerdict.AheadOfSite,
            new ImportCheckDifference("sub10", null, ImportCheckDifferenceKind.Extra, 1)));

        var markup = Render().Markup;

        // A CSV import or a retired chart is not a repair — there is nothing to fetch.
        Assert.DoesNotContain("Add these scores", markup);
    }

    [Fact]
    public void TheDeepScanIsOfferedOnlyWhenTheCensusFoundNothing()
    {
        Answer(Report(ImportCheckVerdict.InSync));
        Assert.Contains("Run a deep scan", Render().Markup);

        Answer(Report(ImportCheckVerdict.MissingScores,
            new ImportCheckDifference("18", 18, ImportCheckDifferenceKind.Missing, 1)));
        // With something localised to repair, the expensive blind walk is not the next step.
        Assert.DoesNotContain("Run a deep scan", Render().Markup);
    }

    [Fact]
    public void AnExhaustedAllowanceNamesTheDateInsteadOfOfferingTheButton()
    {
        Answer(Report(ImportCheckVerdict.InSync), 0);

        var markup = Render().Markup;

        Assert.DoesNotContain("Run a deep scan", markup);
        Assert.Contains("used all 3 deep scans", markup);
    }

    [Fact]
    public void EveryActionIsBlockedWithoutACredential()
    {
        Answer(Report(ImportCheckVerdict.MissingScores,
            new ImportCheckDifference("18", 18, ImportCheckDifferenceKind.Missing, 1)));

        var buttons = Render(credentials: false).FindAll("button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void EveryActionIsBlockedWhileTheImportItselfIsRunning()
    {
        Answer(Report(ImportCheckVerdict.InSync));

        var buttons = Render(busy: true).FindAll("button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void PressingCheckStartsOneAndTheRepairButtonAsksForARepair()
    {
        Answer(Report(ImportCheckVerdict.MissingScores,
            new ImportCheckDifference("18", 18, ImportCheckDifferenceKind.Missing, 1)));
        var started = new List<StartImportCheckCommand>();
        _mediator.Setup(m => m.Send(It.IsAny<StartImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .Callback((object c, CancellationToken _) => started.Add((StartImportCheckCommand)c))
            .ReturnsAsync(new ImportCheckStartResult(ImportCheckStartOutcome.Started, 3));
        var panel = Render();

        panel.FindAll("button").First(b => b.TextContent.Contains("Add these scores")).Click();

        var command = Assert.Single(started);
        Assert.True(command.Repair);
        Assert.False(command.DeepScan);
        Assert.Equal(MixEnum.Phoenix, command.Mix);
    }

    [Fact]
    public void ANeverCheckedAccountExplainsWhatTheCheckDoes()
    {
        Answer(null);

        var markup = Render().Markup;

        Assert.Contains("level by level", markup);
        Assert.DoesNotContain("Add these scores", markup);
    }
}
