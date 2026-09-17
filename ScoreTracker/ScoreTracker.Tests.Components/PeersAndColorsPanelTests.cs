using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.Account;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one place the peer settings change — its own tab on /Account. Pinned: the catalog's sources render with their
///     counts, the tally is the union the reader would compute, PUMBILITY greys where it is empty,
///     and Save writes both settings under their keys.
/// </summary>
public sealed class PeersAndColorsPanelTests : ComponentTestBase
{
    private static IReadOnlySet<PeerVoice> Voices(params Guid[] ids) =>
        ids.Select(PeerVoice.Account).ToHashSet();

    private static readonly Guid Club = Guid.NewGuid();
    private static readonly Guid Region = Guid.NewGuid();
    private static readonly Guid Shared = Guid.NewGuid();
    private static readonly Guid RivalOnly = Guid.NewGuid();
    private static readonly Guid Clubmate = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _settings = new();

    public PeersAndColorsPanelTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _settings.Setup(s => s.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.Phoenix);
        _mediator.Setup(m => m.Send(It.IsAny<GetPeerSourceCatalogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerSourceCatalog(new[]
            {
                new PeerSourceOption(PeerSourceKind.Rivals, null, "", false, false, true,
                    Voices(Shared, RivalOnly), Voices(Shared, RivalOnly), 1),
                new PeerSourceOption(PeerSourceKind.CompetitiveLevel, null, "", false, false, true,
                    Voices(Shared, Guid.NewGuid(), Guid.NewGuid()), Voices(Guid.NewGuid()), 0),
                new PeerSourceOption(PeerSourceKind.Pumbility, null, "", false, false, false,
                    Voices(), Voices(), 0),
                new PeerSourceOption(PeerSourceKind.Community, Club, "NorCal Pump", false, false, true,
                    Voices(Clubmate, Shared), Voices(Clubmate, Shared), 0),
                new PeerSourceOption(PeerSourceKind.Community, Region, "United States", true, false, true,
                    Voices(Clubmate), Voices(Clubmate), 0)
            }));
        // The preview's difficulty bubbles read scoring levels through the mediator this test
        // registers, so the base's stub for that query has to be repeated here.
        _mediator.Setup(m => m.Send(It.IsAny<ScoreTracker.ChartIntelligence.Contracts.Queries.GetChartScoringLevelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<Guid, double>)new Dictionary<Guid, double>());
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_settings.Object);
        this.RenderInteractive();
    }

    private IRenderedFragment RenderPanel()
    {
        return Render(builder =>
        {
            builder.OpenComponent<PeersAndColorsPanel>(0);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ListsEverySourceWithItsCountAndGreysAnEmptyOne()
    {
        var cut = RenderPanel();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("3 players · 1 board-only", cut.Markup);
            Assert.Contains("3 singles · 1 doubles", cut.Markup);
            Assert.Contains("Phoenix 2 only", cut.Markup);
            Assert.Contains("NorCal Pump", cut.Markup);
            Assert.Contains("United States", cut.Markup);
            Assert.Contains("pcd-opt-off", cut.Find("[data-testid='pcd-source-Pumbility']").ClassName);
        });
    }

    [Fact]
    public async Task TheTallyIsTheUnionOfWhatIsTicked()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));
        // The default ticks the competitive band alone: three singles.
        Assert.Contains("3", cut.Find("[data-testid='pcd-tally']").TextContent);

        await cut.Find("[data-testid='pcd-source-Rivals'] input").ChangeAsync(new ChangeEventArgs { Value = true });

        // Two rivals join, one of whom is already in the band: four, not five.
        cut.WaitForAssertion(() => Assert.Contains("4", cut.Find("[data-testid='pcd-tally']").TextContent));
    }

    [Fact]
    public async Task SaveWritesBothSettingsUnderTheirKeys()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));
        await cut.Find("[data-testid='pcd-source-Rivals'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        // MudRadio registers a click, not a change, on its input.
        await cut.Find("[data-testid='pcd-system-Podium'] input").ClickAsync(new MouseEventArgs());

        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(PeerSourceSelection.SettingKey,
            It.Is<string>(v => v.Contains("Rivals") && v.Contains("Competitive")), It.IsAny<CancellationToken>()), Times.Once);
        _settings.Verify(s => s.SetSetting(ScoreColorSettings.SettingKey,
            It.Is<string>(v => v.Contains("system=Podium")), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The difficulty glow's switch sits in the Glow section (hardmode-leaderboard.md D34). It reads the stored
    ///     opt-out, and like everything else on the tab it writes nothing until Save.
    /// </summary>
    [Fact]
    public async Task TheDifficultyGlowSwitchReadsTheOptOutAndSavesWithTheTab()
    {
        _settings.Setup(s => s.GetSetting(DifficultyGlow.SettingKey, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync("true");
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));
        Assert.False(DifficultyGlowSwitch(cut).Value);

        await cut.InvokeAsync(() => DifficultyGlowSwitch(cut).ValueChanged.InvokeAsync(true));
        _settings.Verify(s => s.ClearSetting(DifficultyGlow.SettingKey, It.IsAny<CancellationToken>()), Times.Never);

        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.ClearSetting(DifficultyGlow.SettingKey, It.IsAny<CancellationToken>()), Times.Once);
        _settings.Verify(s => s.SetSetting(DifficultyGlow.SettingKey, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SwitchingTheDifficultyGlowOffWritesTheOptOutOnSave()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));
        Assert.True(DifficultyGlowSwitch(cut).Value);
        // In the Glow section, with the score glow rules.
        Assert.NotNull(cut.FindAll("section.pcd-section").Single(s => s.QuerySelector("[data-testid='pcd-glow-Off']") != null)
            .QuerySelector("[data-testid='pcd-difficulty-glow']"));

        await cut.InvokeAsync(() => DifficultyGlowSwitch(cut).ValueChanged.InvokeAsync(false));
        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(DifficultyGlow.SettingKey, "true", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MudSwitch<bool> DifficultyGlowSwitch(IRenderedFragment cut)
    {
        return cut.FindComponents<MudSwitch<bool>>().Select(s => s.Instance).Single(s => s.Label == "Show difficulty glow");
    }

    /// <summary>
    ///     Leaving a club leaves its id in the setting. The draft keeps only what the catalog can
    ///     offer, so the stale id neither ticks a phantom nor survives the next Save.
    /// </summary>
    [Fact]
    public async Task ACommunityYouLeftIsDroppedFromTheDraftAndFromSave()
    {
        var gone = Guid.NewGuid();
        _settings.Setup(s => s.GetSetting(PeerSourceSelection.SettingKey, default, null))
            .ReturnsAsync(new PeerSourceSelection(false, false, false, new HashSet<Guid> { Club, gone }).Serialize());
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));

        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(PeerSourceSelection.SettingKey,
            It.Is<string>(v => PeerSourceSelection.Parse(v).CommunityIds.SetEquals(new[] { Club })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ThePreviewPaintsSevenSamplesWithTheDraft()
    {
        var cut = RenderPanel();

        cut.WaitForAssertion(() =>
        {
            var preview = cut.Find("[data-testid='pcd-preview']");
            Assert.Equal(7, preview.QuerySelectorAll("[data-testid='peer-score']").Length);
            Assert.Contains("#6 of 94 peers", preview.TextContent);
            Assert.Contains("PG · 12 of 88 peers have it", preview.TextContent);
        });
    }

    private IRenderedFragment RenderLoadedPanel()
    {
        var cut = RenderPanel();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='pcd-tally']")));
        return cut;
    }

    [Fact]
    public async Task AChipSetsItsNumberAndSelectsItsRule()
    {
        var cut = RenderLoadedPanel();

        var chip = cut.FindAll("[data-testid='pcd-glow-UnderPointsToNextGrade'] .mud-chip")
            .Single(c => c.TextContent.Contains((2500).ToString("N0")));
        await chip.ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(ScoreColorSettings.SettingKey,
            It.Is<string>(v => ScoreColorSettings.Parse(v) == new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum,
                GlowRule.UnderPointsToNextGrade, 2500, GlowStrength.One)), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     On a peer rule the 1,000 and 20% chips already read as selected, because each grade rule's chips show
    ///     its number. Tapping one still has to select its rule, though the chip set sees no change to report.
    /// </summary>
    [Theory]
    [InlineData("pcd-glow-UnderPointsToNextGrade", GlowRule.UnderPointsToNextGrade, 1000)]
    [InlineData("pcd-glow-LastPercentOfGrade", GlowRule.LastPercentOfGrade, 20)]
    public async Task AChipAlreadyShowingItsRulesNumberStillSelectsTheRule(string option, GlowRule rule, int threshold)
    {
        var cut = RenderLoadedPanel();
        var highlighted = cut.Find($"[data-testid='{option}'] .mud-chip-selected");

        await highlighted.ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(ScoreColorSettings.SettingKey,
            It.Is<string>(v => ScoreColorSettings.Parse(v).Glow == rule && ScoreColorSettings.Parse(v).GlowThreshold == threshold),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheStrengthChoiceSitsBeneathTheSelectedGradeRuleOnly()
    {
        var cut = RenderLoadedPanel();
        Assert.Empty(cut.FindAll("[data-testid='pcd-glow-strength']"));

        await cut.Find("[data-testid='pcd-glow-UnderPointsToNextGrade'] input[type='radio']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[data-testid='pcd-glow-UnderPointsToNextGrade'] [data-testid='pcd-glow-strength']")));
        Assert.Empty(cut.FindAll("[data-testid='pcd-glow-LastPercentOfGrade'] [data-testid='pcd-glow-strength']"));

        await cut.Find("[data-testid='pcd-glow-LastPercentOfGrade'] input[type='radio']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[data-testid='pcd-glow-LastPercentOfGrade'] [data-testid='pcd-glow-strength']")));
        Assert.Empty(cut.FindAll("[data-testid='pcd-glow-UnderPointsToNextGrade'] [data-testid='pcd-glow-strength']"));
    }

    [Fact]
    public async Task BrighterTheCloserAtAHundredPercentSaysItSpansTheWholeGradeAndSaves()
    {
        var cut = RenderLoadedPanel();

        var hundred = cut.FindAll("[data-testid='pcd-glow-LastPercentOfGrade'] .mud-chip").Last();
        await hundred.ClickAsync(new MouseEventArgs());
        var brighter = cut.FindAll("[data-testid='pcd-glow-strength'] .mud-toggle-item")
            .Single(b => b.TextContent.Contains("Brighter the closer"));
        await brighter.ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Contains("Faint at the bottom of a grade, full at the next one.",
            cut.Find("[data-testid='pcd-glow-strength-caption']").TextContent));

        await cut.Find("[data-testid='pcd-save']").ClickAsync(new MouseEventArgs());

        _settings.Verify(s => s.SetSetting(ScoreColorSettings.SettingKey,
            It.Is<string>(v => ScoreColorSettings.Parse(v) == new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum,
                GlowRule.LastPercentOfGrade, 100, GlowStrength.BrighterTheCloser)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASavedGradeRuleOpensSelectedWithItsNumberAndStrength()
    {
        _settings.Setup(s => s.GetSetting(ScoreColorSettings.SettingKey, default, null))
            .ReturnsAsync(new ScoreColorSettings(ScoreColorSystem.JudgementSpectrum, GlowRule.UnderPointsToNextGrade, 750,
                GlowStrength.BrighterTheCloser).Serialize());

        var cut = RenderLoadedPanel();

        cut.WaitForAssertion(() =>
        {
            var caption = cut.Find("[data-testid='pcd-glow-UnderPointsToNextGrade'] [data-testid='pcd-glow-strength-caption']");
            Assert.Contains($"Faint at {(750).ToString("N0")} points away, full at the next grade.", caption.TextContent);
        });
    }
}
