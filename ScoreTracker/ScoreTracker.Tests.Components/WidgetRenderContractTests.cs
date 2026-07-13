using System.Reflection;
using Microsoft.AspNetCore.Components;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Home-widget render params are bound reflectively by <c>DynamicComponent</c> in
///     <c>WidgetHost</c>, so a widget that misses a parameter or declares the wrong type still
///     compiles — and only blows up at render (unknown-parameter / <see cref="InvalidCastException" />).
///     That trap has bitten a main-merge before (the shell moved <c>OnChartClick</c> from
///     <c>EventCallback&lt;Chart&gt;</c> to <c>EventCallback&lt;ChartClickContext&gt;</c> and added
///     <c>RefreshToken</c>). This pins the five-param contract for every registered widget so a shell
///     change — or a new widget — can't ship a render crash. See home-page-widgets.md §2.2.
/// </summary>
public sealed class WidgetRenderContractTests
{
    private static readonly (string Name, Type Type)[] Contract =
    {
        ("Widget", typeof(HomePageWidgetRecord)),
        ("EffectiveMix", typeof(MixEnum)),
        ("EditMode", typeof(bool)),
        ("OnChartClick", typeof(EventCallback<ChartClickContext>)),
        ("RefreshToken", typeof(int))
    };

    [Fact]
    public void EveryRegisteredWidgetDeclaresTheRenderContract()
    {
        foreach (var descriptor in WidgetRegistry.All)
        {
            var parameters = descriptor.RenderComponent
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() != null)
                .ToDictionary(p => p.Name, p => p.PropertyType);

            foreach (var (name, type) in Contract)
            {
                Assert.True(parameters.TryGetValue(name, out var actual),
                    $"{descriptor.RenderComponent.Name} ({descriptor.TypeId}) is missing [Parameter] {name} — " +
                    "DynamicComponent would throw at render.");
                Assert.True(actual == type,
                    $"{descriptor.RenderComponent.Name} ({descriptor.TypeId}).{name} is {actual}, expected {type}.");
            }
        }
    }
}
