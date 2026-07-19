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

    protected ComponentTestBase()
    {
        Services.AddSingleton(CurrentUser.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        // No MudPopoverProvider in a component-under-test's tree; tooltips render their
        // activator content regardless, which is all these facts assert on.
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddSingleton(mediator.Object);
        Services.AddScoped<ChartScoringLevels>();

        // The shared LeaderboardDialog reads the relevant-players setting; a loose stub keeps
        // every consumer renderable (tests that assert on the setting register their own).
        Services.AddSingleton(Mock.Of<IUiSettingsAccessor>());

        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        Services.AddSingleton(localizer.Object);
    }
}
