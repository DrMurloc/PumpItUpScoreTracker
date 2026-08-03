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
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Score check panel. What matters here: it starts with no verdict (nothing is stored), it
///     names charts rather than counts, missing and out-of-date share ONE list, and the deep scan
///     is gated on the month's balance.
/// </summary>
public sealed class ScoreCheckPanelTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _missingChart = Guid.NewGuid();
    private readonly Guid _staleChart = Guid.NewGuid();
    private readonly UiNotificationHub _hub = new();

    public ScoreCheckPanelTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton<IUiNotificationHub>(_hub);
        Services.AddSingleton(Mock.Of<ISnackbar>());

        _mediator.Setup(m => m.Send(It.IsAny<GetDeepScansRemainingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Chart(_missingChart, "Ugly duck Toccata", 17),
                Chart(_staleChart, "The End of the World", 20)
            });
    }

    private static Chart Chart(Guid id, string song, int level)
    {
        return new Chart(id, MixEnum.Phoenix,
            new Song(Name.From(song), SongType.Arcade, new Uri("https://example.invalid/song.png"),
                TimeSpan.FromMinutes(2), Name.From("Artist"), null),
            ScoreTracker.SharedKernel.Enums.ChartType.Single, DifficultyLevel.From(level), MixEnum.Phoenix,
            null, null, new HashSet<Skill>());
    }

    private IRenderedComponent<ScoreCheckPanel> Render(bool credentials = true, bool busy = false)
    {
        // Named findings render a DifficultyBubble and a SongImage, both of which branch on
        // RendererInfo.
        this.RenderInteractive();
        return RenderComponent<ScoreCheckPanel>(p => p
            .Add(c => c.Mix, MixEnum.Phoenix)
            .Add(c => c.CardId, "card")
            .Add(c => c.GameTag, "TAG #1")
            .Add(c => c.Busy, busy)
            .Add(c => c.Credentials, () => credentials ? new TypedCredentialSource("user", "pass") : null));
    }

    /// <summary>Delivers a finished check the way the saga does — over the hub, never from storage.</summary>
    private async Task Complete(IRenderedComponent<ScoreCheckPanel> panel, ImportCheckReport report,
        int repaired = 0)
    {
        await panel.InvokeAsync(() => _hub.PublishAsync(UiTopics.User(_me),
            new ImportCheckCompletedEvent(_me, MixEnum.Phoenix, report, repaired)));
    }

    private static ImportCheckReport Report(params ImportCheckDifference[] differences)
    {
        return new ImportCheckReport(MixEnum.Phoenix,
            differences.Any(d => d.Kind != ImportCheckDifferenceKind.Extra)
                ? ImportCheckVerdict.NeedsAttention
                : ImportCheckVerdict.InSync,
            64466, 63420, 2851, 2848, differences);
    }

    private ImportCheckDifference Missing(int level, Guid chartId, int score)
    {
        return new ImportCheckDifference(level.ToString(), level, ImportCheckDifferenceKind.Missing, 1,
            new[] { new ImportCheckChart(chartId, score, null) });
    }

    private ImportCheckDifference Stale(int level, Guid chartId, int score, int currentScore)
    {
        return new ImportCheckDifference(level.ToString(), level, ImportCheckDifferenceKind.OutOfDate, 1,
            new[] { new ImportCheckChart(chartId, score, currentScore) });
    }

    [Fact]
    public void ItStartsWithNoVerdictBecauseNothingIsStored()
    {
        var markup = Render().Markup;

        Assert.Contains("Check every score", markup);
        Assert.DoesNotContain("Everything matches", markup);
        Assert.DoesNotContain("Add these", markup);
    }

    [Fact]
    public async Task MissingAndOutOfDateSharUneList()
    {
        var panel = Render();

        await Complete(panel, Report(
            Missing(17, _missingChart, 996408),
            Stale(20, _staleChart, 992223, 966204)));

        // An account can be short a chart and behind on another at once; two views would make the
        // player fix the same account twice.
        var markup = panel.Markup;
        Assert.Contains("2 scores here don't match PIUGAME.", markup);
        Assert.Contains("Ugly duck Toccata", markup);
        Assert.Contains("The End of the World", markup);
        Assert.Contains("Add these 2 scores", markup);
    }

    [Fact]
    public async Task OnlyAnOutOfDateRowCarriesTheScoreWeAlreadyHold()
    {
        var panel = Render();

        await Complete(panel, Report(
            Missing(17, _missingChart, 996408),
            Stale(20, _staleChart, 992223, 966204)));

        // The "was" IS the distinction between the two kinds — no extra label needed.
        Assert.Contains("996,408", panel.Markup);
        Assert.Contains("was 966,204", panel.Markup);
        Assert.DoesNotContain("was 996,408", panel.Markup);
    }

    [Fact]
    public async Task AnInSyncAccountSaysSoAndOffersTheDeepScan()
    {
        var panel = Render();

        await Complete(panel, Report());

        Assert.Contains("Everything matches", panel.Markup);
        Assert.Contains("Run a deep scan", panel.Markup);
        Assert.DoesNotContain("Add these", panel.Markup);
    }

    [Fact]
    public async Task WithSomethingToFixTheExpensiveBlindWalkIsNotOffered()
    {
        var panel = Render();

        await Complete(panel, Report(Missing(17, _missingChart, 996408)));

        Assert.DoesNotContain("Run a deep scan", panel.Markup);
    }

    [Fact]
    public async Task AnExhaustedAllowanceNamesTheUnlockInsteadOfOfferingTheButton()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetDeepScansRemainingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var panel = Render();

        await Complete(panel, Report());

        Assert.DoesNotContain("Run a deep scan", panel.Markup);
        Assert.Contains("used all your deep scans", panel.Markup);
    }

    [Fact]
    public async Task HoldingMoreThanPiuGameIsNeverOfferedAsSomethingToFix()
    {
        var panel = Render();

        await Complete(panel, Report(new ImportCheckDifference("sub10", null,
            ImportCheckDifferenceKind.Extra, 1, Array.Empty<ImportCheckChart>())));

        // A CSV import or a retired chart is not a repair — there is nothing to fetch.
        Assert.DoesNotContain("Add these", panel.Markup);
    }

    [Fact]
    public async Task ARepairThatFixedEverythingSaysWhatItDid()
    {
        var panel = Render();

        await Complete(panel, Report(), 5);

        Assert.Contains("Added 5 scores", panel.Markup);
    }

    [Fact]
    public async Task TheRepairAsksForExactlyTheLevelsThisVerdictNamed()
    {
        var started = new List<StartImportCheckCommand>();
        _mediator.Setup(m => m.Send(It.IsAny<StartImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .Callback((object c, CancellationToken _) => started.Add((StartImportCheckCommand)c))
            .ReturnsAsync(new ImportCheckStartResult(ImportCheckStartOutcome.Started, 3));
        var panel = Render();
        await Complete(panel, Report(Missing(17, _missingChart, 996408), Stale(20, _staleChart, 992223, 966204)));

        panel.FindAll("button").First(b => b.TextContent.Contains("Add these")).Click();

        // The panel holds the findings — nothing on the server remembers the last run.
        var command = Assert.Single(started);
        Assert.Equal(new[] { "17", "20" }, command.RepairBuckets);
        Assert.False(command.DeepScan);
    }

    [Fact]
    public void EveryActionIsBlockedWithoutACredential()
    {
        var buttons = Render(credentials: false).FindAll("button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void EveryActionIsBlockedWhileTheImportItselfIsRunning()
    {
        var buttons = Render(busy: true).FindAll("button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void TheImportItRunsFirstIsStatedBeforeTheButtonIsPressed()
    {
        // A field tester was surprised by session charts appearing: the check imports first.
        Assert.Contains("Imports first", Render().Markup);
    }
}
