using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The per-circuit difficulty glow (docs/design/hardmode-leaderboard.md D24, D32): red for the
///     week's Hardmode charts, mint for each folder's most held.
/// </summary>
public sealed class DifficultyGlowTests
{
    private static readonly Guid Hard = Guid.NewGuid();
    private static readonly Guid Easy = Guid.NewGuid();
    private static readonly Guid Neither = Guid.NewGuid();

    [Fact]
    public async Task EachEndOfTheFolderGetsItsOwnGlow()
    {
        var glow = Build(Settings(null), loggedIn: true);

        Assert.Equal(DifficultyGlowKind.Hard, await glow.GlowFor(MixEnum.Phoenix2, Hard));
        Assert.Equal(DifficultyGlowKind.Easy, await glow.GlowFor(MixEnum.Phoenix2, Easy));
        Assert.Equal(DifficultyGlowKind.None, await glow.GlowFor(MixEnum.Phoenix2, Neither));
    }

    [Fact]
    public async Task AChartInBothListsGlowsRed()
    {
        // The census keeps the ends apart; if a chart ever reached both, calling a Hardmode chart
        // "most held" would be the lie, so red wins.
        var reader = Reader();
        reader.Setup(h => h.GetMostHeldCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(Hard) });
        var glow = Build(Settings(null), loggedIn: true, reader);

        Assert.Equal(DifficultyGlowKind.Hard, await glow.GlowFor(MixEnum.Phoenix2, Hard));
    }

    [Fact]
    public async Task ASignedOutViewerGetsTheDefaultWithoutTouchingSettings()
    {
        // ⚠ The bug this pins cost a 500 on every statically-rendered chart page: the settings
        // accessor's anonymous path is ProtectedBrowserStorage, which is JS interop and throws
        // outside a live circuit. The difficulty bubble renders on static pages, so the glow has
        // to answer without asking. Caught by E2E, not by any mocked suite.
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ThrowsAsync(new InvalidOperationException("JavaScript interop calls cannot be issued at this time"));

        var glow = Build(settings, loggedIn: false);

        Assert.Equal(DifficultyGlowKind.Hard, await glow.GlowFor(MixEnum.Phoenix2, Hard));
        Assert.Equal(DifficultyGlowKind.Easy, await glow.GlowFor(MixEnum.Phoenix2, Easy));
        settings.Verify(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task ASignedInViewerWhoTurnedTheGlowOffSeesNeitherEnd()
    {
        // The stored key is still the one "Mark Hardmode charts" wrote, so an old opt-out carries.
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting("Universal__HideHardmodeMark", It.IsAny<CancellationToken>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync("true");

        var glow = Build(settings, loggedIn: true);

        Assert.Equal(DifficultyGlow.SettingKey, "Universal__HideHardmodeMark");
        Assert.Equal(DifficultyGlowKind.None, await glow.GlowFor(MixEnum.Phoenix2, Hard));
        Assert.Equal(DifficultyGlowKind.None, await glow.GlowFor(MixEnum.Phoenix2, Easy));
    }

    [Fact]
    public async Task TheSettingAndBothListsAreReadOnceHoweverManyBubblesAsk()
    {
        // Forty bubbles on a page, one read of each - the whole reason this is a service rather
        // than a lookup in the component.
        var settings = Settings(null);
        var reader = Reader();
        var glow = Build(settings, loggedIn: true, reader);

        await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(i => glow.GlowFor(MixEnum.Phoenix2, i % 2 == 0 ? Hard : Easy)));

        settings.Verify(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()),
            Times.Once);
        reader.Verify(h => h.GetQualifyingCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()), Times.Once);
        reader.Verify(h => h.GetMostHeldCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ATransientReadFailureDoesNotPoisonTheCircuit()
    {
        // ⚠ Memoizing the TASK would cache the fault for the life of the circuit - hours - and
        // every later bubble would rethrow from OnParametersSetAsync, killing the circuit on
        // each reconnect, on nearly every page of the site. ChartScoringLevels stores the
        // awaited value for exactly this reason. Bug check, 2026-09-15.
        var reader = Reader();
        var calls = 0;
        reader.Setup(h => h.GetMostHeldCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .Returns(() => ++calls == 1
                ? Task.FromException<IReadOnlyList<HardmodeChartEntry>>(new TimeoutException("transient"))
                : Task.FromResult<IReadOnlyList<HardmodeChartEntry>>(new[] { Entry(Easy) }));
        var glow = Build(Settings(null), loggedIn: true, reader);

        await Assert.ThrowsAsync<TimeoutException>(() => glow.GlowFor(MixEnum.Phoenix2, Easy));

        // The next render retries rather than rethrowing the first failure forever.
        Assert.Equal(DifficultyGlowKind.Easy, await glow.GlowFor(MixEnum.Phoenix2, Easy));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AMixWithNoCensusGlowsNothing()
    {
        var glow = Build(Settings(null), loggedIn: true);

        Assert.Equal(DifficultyGlowKind.None, await glow.GlowFor(MixEnum.Phoenix, Hard));
        Assert.Equal(DifficultyGlowKind.None, await glow.GlowFor(MixEnum.Phoenix, Easy));
    }

    private static HardmodeChartEntry Entry(Guid chartId) =>
        new(chartId, ChartType.Single, 21, 0, 0, 40, 10);

    private static Mock<IUiSettingsAccessor> Settings(string? stored)
    {
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync(stored);
        return settings;
    }

    /// <summary>Phoenix 2 carries one chart at each end; every other mix has no census.</summary>
    private static Mock<IHardmodeChartReader> Reader()
    {
        var reader = new Mock<IHardmodeChartReader>();
        reader.Setup(h => h.GetQualifyingCharts(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HardmodeChartEntry>());
        reader.Setup(h => h.GetMostHeldCharts(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HardmodeChartEntry>());
        reader.Setup(h => h.GetQualifyingCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(Hard) });
        reader.Setup(h => h.GetMostHeldCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(Easy) });
        return reader;
    }

    private static DifficultyGlow Build(Mock<IUiSettingsAccessor> settings, bool loggedIn,
        Mock<IHardmodeChartReader>? reader = null)
    {
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(loggedIn);
        return new DifficultyGlow((reader ?? Reader()).Object, settings.Object, currentUser.Object);
    }
}
