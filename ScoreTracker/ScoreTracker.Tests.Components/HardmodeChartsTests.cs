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
///     The per-circuit Hardmode mark (docs/design/hardmode-leaderboard.md D24).
/// </summary>
public sealed class HardmodeChartsTests
{
    private static readonly Guid Chart = Guid.NewGuid();

    [Fact]
    public async Task ASignedOutViewerGetsTheDefaultWithoutTouchingSettings()
    {
        // ⚠ The bug this pins cost a 500 on every statically-rendered chart page: the settings
        // accessor's anonymous path is ProtectedBrowserStorage, which is JS interop and throws
        // outside a live circuit. The difficulty bubble renders on static pages, so the mark has
        // to answer without asking. Caught by E2E, not by any mocked suite.
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ThrowsAsync(new InvalidOperationException("JavaScript interop calls cannot be issued at this time"));

        var marks = Build(settings, loggedIn: false, qualifying: Chart);

        Assert.True(await marks.IsMarked(MixEnum.Phoenix2, Chart));
        settings.Verify(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task ASignedInViewerWhoOptedOutMarksNothing()
    {
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(HardmodeCharts.SettingKey, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync("true");

        var marks = Build(settings, loggedIn: true, qualifying: Chart);

        Assert.False(await marks.IsMarked(MixEnum.Phoenix2, Chart));
    }

    [Fact]
    public async Task TheSettingIsReadOnceHoweverManyBubblesAsk()
    {
        // Forty bubbles on a page, one read - the whole reason this is a service rather than a
        // lookup in the component.
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        var reader = new Mock<IHardmodeChartReader>();
        reader.Setup(h => h.GetQualifyingCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(Chart) });
        var marks = Build(settings, loggedIn: true, reader: reader);

        await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(_ => marks.IsMarked(MixEnum.Phoenix2, Chart)));

        settings.Verify(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()),
            Times.Once);
        reader.Verify(h => h.GetQualifyingCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ATransientReadFailureDoesNotPoisonTheCircuit()
    {
        // ⚠ Memoizing the TASK would cache the fault for the life of the circuit - hours - and
        // every later bubble would rethrow from OnParametersSetAsync, killing the circuit on
        // each reconnect, on nearly every page of the site. ChartScoringLevels stores the
        // awaited value for exactly this reason. Bug check, 2026-09-15.
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        var reader = new Mock<IHardmodeChartReader>();
        var calls = 0;
        reader.Setup(h => h.GetQualifyingCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .Returns(() => ++calls == 1
                ? Task.FromException<IReadOnlyList<HardmodeChartEntry>>(new TimeoutException("transient"))
                : Task.FromResult<IReadOnlyList<HardmodeChartEntry>>(new[] { Entry(Chart) }));
        var marks = Build(settings, loggedIn: true, reader: reader);

        await Assert.ThrowsAsync<TimeoutException>(() => marks.IsMarked(MixEnum.Phoenix2, Chart));

        // The next render retries rather than rethrowing the first failure forever.
        Assert.True(await marks.IsMarked(MixEnum.Phoenix2, Chart));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AMixWithNoCensusMarksNothing()
    {
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);

        var marks = Build(settings, loggedIn: true);

        Assert.False(await marks.IsMarked(MixEnum.Phoenix, Chart));
    }

    private static HardmodeChartEntry Entry(Guid chartId) =>
        new(chartId, ChartType.Single, 21, 0, 0, 40, 10);

    private static HardmodeCharts Build(Mock<IUiSettingsAccessor> settings, bool loggedIn,
        Mock<IHardmodeChartReader>? reader = null, params Guid[] qualifying)
    {
        reader ??= new Mock<IHardmodeChartReader>();
        if (qualifying.Length > 0)
            reader.Setup(h => h.GetQualifyingCharts(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(qualifying.Select(Entry).ToArray());
        reader.Setup(h => h.GetQualifyingCharts(It.Is<MixEnum>(m => m != MixEnum.Phoenix2),
            It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<HardmodeChartEntry>());

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(loggedIn);
        return new HardmodeCharts(reader.Object, settings.Object, currentUser.Object);
    }
}
