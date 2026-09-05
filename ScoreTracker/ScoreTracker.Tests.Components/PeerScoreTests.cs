using System;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one component for "your score, colored by your standing". Pinned: the color and the
///     printed standing come from the same standing, a tap opens the popover without reaching the
///     host, a chart nobody measured renders plain, and the player's saved color system is what
///     paints it.
/// </summary>
public sealed class PeerScoreTests : ComponentTestBase
{
    private readonly Mock<IUiSettingsAccessor> _settings = new();

    public PeerScoreTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        // Replaces the base's loose stub before anything resolves it: the preferences read the
        // color system through this.
        Services.AddSingleton(_settings.Object);
        this.RenderInteractive();
    }

    private static PeerStanding Standing(int better, int passed = 93) =>
        new(120, passed, better, 0, 4,
            new[] { new PeerStandingSource(PeerSourceKind.CompetitiveLevel, null, null, false, false, 120, passed, better, 0) },
            null);

    private static Chart TestChart() => new(Guid.NewGuid(), MixEnum.Phoenix,
        new Song(Name.From("Cleaner"), SongType.Arcade, new Uri("https://piuimages.arroweclip.se/probe.png"),
            TimeSpan.Zero, Name.From("Probe"), null),
        ChartType.Single, DifficultyLevel.From(20), MixEnum.Phoenix, null, null);

    private IRenderedComponent<PeerScore> Render(int score, PeerStanding? standing, bool broken = false)
    {
        return RenderComponent<PeerScore>(p => p
            .Add(x => x.Score, PhoenixScore.From(score))
            .Add(x => x.IsBroken, broken)
            .Add(x => x.Mix, MixEnum.Phoenix)
            .Add(x => x.Chart, TestChart())
            .Add(x => x.Standing, standing));
    }

    [Fact]
    public void PaintsTheScoreAndPrintsThePlaceFromTheSameStanding()
    {
        var cut = Render(972_000, Standing(better: 5));

        var score = cut.Find("[data-testid='peer-score']");
        Assert.Contains("color:var(--rarity-sapphire);", score.GetAttribute("style"));
        Assert.Contains("#6 of 94 peers", cut.Find("[data-testid='peer-standing']").TextContent);
    }

    [Fact]
    public void AChartNobodyMeasuredRendersPlainAndPrintsNoStanding()
    {
        var cut = Render(972_000, null);

        Assert.Equal(string.Empty, cut.Find("[data-testid='peer-score']").GetAttribute("style") ?? string.Empty);
        Assert.Empty(cut.FindAll("[data-testid='peer-standing']"));
    }

    [Fact]
    public void ABrokenRunIsNeitherPaintedNorATarget()
    {
        var cut = Render(812_000, Standing(better: 5), broken: true);

        var score = cut.Find("[data-testid='peer-score']");
        Assert.Null(score.GetAttribute("role"));
        Assert.Empty(cut.FindAll("[data-testid='peer-standing']"));
    }

    [Fact]
    public async Task ATapOpensThePopoverAndDoesNotReachTheHost()
    {
        var hostClicks = 0;
        var cut = Render(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => hostClicks++));
            builder.OpenComponent<PeerScore>(2);
            builder.AddAttribute(3, nameof(PeerScore.Score), PhoenixScore.From(972_000));
            builder.AddAttribute(4, nameof(PeerScore.Chart), TestChart());
            builder.AddAttribute(5, nameof(PeerScore.Standing), Standing(better: 5));
            builder.CloseComponent();
            builder.CloseElement();
        });

        await cut.Find("[data-testid='peer-score']").ClickAsync(new MouseEventArgs());

        Assert.Equal("true", cut.Find("[data-testid='peer-score']").GetAttribute("aria-expanded"));
        Assert.Equal(0, hostClicks);
    }

    /// <summary>
    ///     Another player's sessions page paints their scores against the competitive default,
    ///     whatever the viewer ticked. A viewer who un-ticked everything used to tap a colored,
    ///     captioned score and read "you have no peer groups selected": the empty state is the
    ///     standing's to declare, and the setting's only when nothing measured the score.
    /// </summary>
    [Fact]
    public async Task AMeasuredScoreNeverSaysTheViewerChoseNothing()
    {
        _settings.Setup(s => s.GetSetting(PeerSourceSelection.SettingKey, default, null))
            .ReturnsAsync(PeerSourceSelection.Nothing.Serialize());

        var measured = RenderWithPopovers(Standing(better: 5));
        await measured.Find("[data-testid='peer-score']").ClickAsync(new MouseEventArgs());

        measured.WaitForAssertion(() =>
            Assert.True(measured.FindComponent<PeerStandingPopover>().Instance.SourcesChosen));
    }

    /// <summary>The setting decides only for a score nothing measured: that one still says why it is plain.</summary>
    [Fact]
    public async Task AnUnmeasuredScoreWithNothingTickedSaysSo()
    {
        _settings.Setup(s => s.GetSetting(PeerSourceSelection.SettingKey, default, null))
            .ReturnsAsync(PeerSourceSelection.Nothing.Serialize());

        var unmeasured = RenderWithPopovers(null);
        await unmeasured.Find("[data-testid='peer-score']").ClickAsync(new MouseEventArgs());

        unmeasured.WaitForAssertion(() =>
            Assert.False(unmeasured.FindComponent<PeerStandingPopover>().Instance.SourcesChosen));
    }

    /// <summary>The Table density's Better Than cell opens the score's popover through this (D17).</summary>
    [Fact]
    public async Task AHostCanOpenThePopoverFromItsOwnTrigger()
    {
        var cut = RenderWithPopovers(Standing(better: 5));
        var score = cut.FindComponent<PeerScore>();

        await score.InvokeAsync(() => score.Instance.Open());

        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("[data-testid='peer-score']").GetAttribute("aria-expanded")));
    }

    /// <summary>MudPopover renders its content through the provider, so the popover needs one in the tree — and one only, per test.</summary>
    private IRenderedFragment RenderWithPopovers(PeerStanding? standing)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<PeerScore>(1);
            builder.AddAttribute(2, nameof(PeerScore.Score), PhoenixScore.From(972_000));
            builder.AddAttribute(3, nameof(PeerScore.Chart), TestChart());
            builder.AddAttribute(4, nameof(PeerScore.Standing), standing);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void TheSavedColorSystemIsWhatPaintsIt()
    {
        _settings.Setup(s => s.GetSetting(ScoreColorSettings.SettingKey, default, null))
            .ReturnsAsync(new ScoreColorSettings(ScoreColorSystem.Podium, GlowRule.TopPlaces, 1).Serialize());

        var cut = Render(972_000, Standing(better: 1));

        var style = cut.Find("[data-testid='peer-score']").GetAttribute("style");
        Assert.Contains("--plate-mg", style);
    }

    [Fact]
    public void ThePerfectGameLinePrintsWhoElseHoldsIt()
    {
        var pg = new PeerStanding(120, 93, 0, 3, 0, Array.Empty<PeerStandingSource>(), null);

        var cut = Render(1_000_000, pg);

        Assert.Contains("PG · 3 of 93 peers have it", cut.Find("[data-testid='peer-standing']").TextContent);
    }

    [Fact]
    public void TheGlowIsOneClassAndOffIsOff()
    {
        _settings.Setup(s => s.GetSetting(ScoreColorSettings.SettingKey, default, null))
            .ReturnsAsync(new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.Off, 10).Serialize());

        var cut = Render(1_000_000, Standing(better: 0));

        Assert.DoesNotContain("rarity-glow", cut.Find("[data-testid='peer-score']").ClassName);
    }
}
