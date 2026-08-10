using System;
using System.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Every widget descriptor has declared SupportedMixes since the registry was written,
///     and nothing read it: the picker offered all eleven on every mix, and the host rendered
///     whatever it was given. On a legacy mix the Phoenix-shaped ones came up empty, because
///     a competitive level, a Pumbility pool and a weekly board do not exist in a mix whose
///     scores are era-scale numbers with a letter grade.
/// </summary>
public sealed class WidgetMixSupportTests : ComponentTestBase
{
    public WidgetMixSupportTests()
    {
        // The host subscribes score-refreshing widgets to the viewer's own topic, so it needs
        // a real user id even though nothing here publishes.
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(u => u.User)
            .Returns(new User(Guid.NewGuid(), "Me", true, null, new Uri("https://piu.test/me.png"), null));
        Services.AddSingleton(Mock.Of<IUiNotificationHub>());
    }

    /// <summary>
    ///     The three recording surfaces work on every mix by design, and they are the whole
    ///     reason a legacy dashboard is worth reaching: record a score, import a spreadsheet,
    ///     see the breakdown. If this shrinks, a legacy player's dashboard is empty.
    /// </summary>
    [Theory]
    [InlineData("quick-record")]
    [InlineData("import-scores")]
    [InlineData("by-level-breakdown")]
    public void TheEveryMixWidgetsSupportLegacyMixes(string typeId)
    {
        var descriptor = WidgetRegistry.TryGet(typeId);

        Assert.NotNull(descriptor);
        Assert.Contains(MixEnum.Prime2, descriptor!.SupportedMixes);
        Assert.Contains(MixEnum.XX, descriptor.SupportedMixes);
        Assert.Contains(MixEnum.FirstDanceFloor, descriptor.SupportedMixes);
    }

    /// <summary>Phoenix keeps everything — the filter must not cost the mixes that work.</summary>
    [Fact]
    public void EveryWidgetIsAvailableOnPhoenix()
    {
        Assert.All(WidgetRegistry.All, d => Assert.Contains(MixEnum.Phoenix, d.SupportedMixes));
    }

    /// <summary>
    ///     A widget already on a page keeps its slot on a mix it cannot render and says so.
    ///     Dropping it would silently rearrange a dashboard the player built, and bring it back
    ///     on the next mix switch — which reads as a bug rather than a rule.
    /// </summary>
    [Fact]
    public void AnUnsupportedWidgetKeepsItsSlotAndSaysWhyItIsEmpty()
    {
        var cut = RenderHost("pumbility", MixEnum.Prime2);

        Assert.Contains("Not available on Prime 2.", cut.Markup);
        // Still a widget card, not a hole in the grid.
        Assert.Single(cut.FindAll(".dash-widget"));
    }

    /// <summary>
    ///     The host stops before instantiating the widget rather than rendering it and letting
    ///     it come up empty. Proven by what a real render costs: on a mix it supports, the
    ///     Pumbility widget demands its own services and this bare context cannot supply them,
    ///     so the render throws. On a mix it does not support, the same render succeeds —
    ///     which is only possible if the component was never constructed.
    /// </summary>
    [Fact]
    public void AnUnsupportedWidgetIsNeverConstructed()
    {
        Assert.Throws<InvalidOperationException>(() => RenderHost("pumbility", MixEnum.Phoenix));

        var cut = RenderHost("pumbility", MixEnum.Prime2);

        Assert.Contains("Not available on", cut.Markup);
    }

    private IRenderedComponent<WidgetHost> RenderHost(string typeId, MixEnum mix) =>
        RenderComponent<WidgetHost>(p => p
            .Add(h => h.Widget, new HomePageWidgetRecord(Guid.NewGuid(), typeId, null, 0, "1x1", "{}", 1))
            .Add(h => h.EffectiveMix, mix));
}
