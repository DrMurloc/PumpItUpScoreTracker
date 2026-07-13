using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     WidgetHost supplies a fixed set of parameters to every widget by reflection
///     (DynamicComponent), so a widget that fails to declare one compiles cleanly and throws only
///     at render — Community Highlights hit this twice across a shell-contract merge (OnChartClick's
///     type change and the new RefreshToken). This ratchet asserts every registered widget declares
///     the full render contract (§2.2). If WidgetHost.BuildParameters gains a parameter, update this
///     Contract list AND every widget in the same PR.
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
    public void EveryRegisteredWidgetDeclaresTheFullRenderContract()
    {
        var failures = (
            from descriptor in WidgetRegistry.All
            from param in Contract
            let prop = descriptor.RenderComponent.GetProperty(param.Name,
                BindingFlags.Public | BindingFlags.Instance)
            where prop == null
                  || prop.PropertyType != param.Type
                  || !prop.IsDefined(typeof(ParameterAttribute), inherit: true)
            select $"{descriptor.RenderComponent.Name} must declare [Parameter] {param.Type.Name} {param.Name}"
        ).ToArray();

        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }
}
