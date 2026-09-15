using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     DifficultyBubble injects <see cref="HardmodeCharts" /> on every render, so any suite that
///     renders anything containing a chart needs it registered — including the suites that build
///     their own service collection instead of inheriting <see cref="ComponentTestBase" />.
///     <para>
///         One helper rather than four copies: bunit freezes the provider on first resolve, so a
///         suite that forgets this fails at RENDER with "no registered service", pointing at the
///         component rather than at the missing line.
///     </para>
/// </summary>
internal static class HardmodeStub
{
    /// <summary>
    ///     Registers an empty Hardmode list — a mix with no census, so nothing glows unless the
    ///     suite says otherwise. Adds a loose UiSettings stub only where one is missing, since the
    ///     mark reads its opt-out from there.
    /// </summary>
    public static IServiceCollection AddHardmodeStub(this IServiceCollection services)
    {
        var reader = new Mock<IHardmodeChartReader>();
        reader.Setup(h => h.GetQualifyingCharts(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HardmodeChartEntry>());
        services.AddSingleton(reader.Object);
        services.TryAddUiSettings();
        services.AddScoped<HardmodeCharts>();
        return services;
    }

    private static void TryAddUiSettings(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IUiSettingsAccessor))) return;
        services.AddSingleton(new Mock<IUiSettingsAccessor>().Object);
    }
}
