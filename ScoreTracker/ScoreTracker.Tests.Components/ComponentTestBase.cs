using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.Theming;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Shared bUnit context: Mud services, loose JS interop, a pass-through localizer
///     (keys are English UI text verbatim, so the key IS the display string), and the
///     one mediator query DifficultyBubble's scoring-level cache issues.
/// </summary>
public abstract class ComponentTestBase : TestContext
{
    /// <summary>Configure before rendering; components see it through DI.</summary>
    protected Mock<ICurrentUserAccessor> CurrentUser { get; } = new();

    /// <summary>
    ///     The shared dispatcher. Tests add setups to THIS rather than registering their own:
    ///     bunit freezes the service provider on first render, so a late AddSingleton throws.
    /// </summary>
    protected Mock<IMediator> Mediator { get; } = new();

    /// <summary>
    ///     The shared UI-settings store, for the same reason: a suite whose constructor renders —
    ///     anything calling <c>RenderInteractive()</c> — has frozen the provider before its first
    ///     test body runs, so a setting can only be stubbed through a mock that is already in it.
    /// </summary>
    protected Mock<IUiSettingsAccessor> UiSettings { get; } = new();

    protected ComponentTestBase()
    {
        Services.AddSingleton(CurrentUser.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        // No MudPopoverProvider in a component-under-test's tree; tooltips render their
        // activator content regardless, which is all these facts assert on.
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);

        Mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddSingleton(Mediator.Object);
        Services.AddScoped<ChartScoringLevels>();

        // The shared LeaderboardDialog reads the relevant-players setting; an unconfigured mock
        // answers every getter with its default and keeps every consumer renderable. A suite that
        // registers its own before rendering still wins — the later registration is the one
        // resolved — and a suite that cannot register stubs this one instead.
        Services.AddSingleton(UiSettings.Object);
        // PeerScore reads the viewer's peer and color settings through this; the loose settings
        // stub above makes it answer the defaults (competitive alone, the judgement spectrum).
        Services.AddScoped<ScoreColorPreferences>();

        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        Services.AddSingleton(localizer.Object);
    }
}
