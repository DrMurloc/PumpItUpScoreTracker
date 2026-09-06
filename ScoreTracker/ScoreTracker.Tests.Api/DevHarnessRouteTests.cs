using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ScoreTracker.Data.DevTooling;
using ScoreTracker.Web.Controllers.Api.V2;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     Every URL the local-dev harness requests resolves to a route the app actually registers.
///     <para>
///         This exists because it did not. The harness asked for
///         <c>api/v2/chart-analysis/chart-scoring-levels</c> — a path that was never registered —
///         and since that call does not tolerate a miss, <c>EnsureSuccessStatusCode</c> threw and
///         <c>/Dev/Populate</c> failed outright. It survived two commits because the seeder was
///         tested against a real database and the reader was not tested at all.
///     </para>
///     <para>
///         Cheap by construction: reflection over the controllers' route attributes, no server, no
///         database. It cannot catch a wrong query parameter, but it catches the whole class of
///         defect where a path drifts from the controller that serves it.
///     </para>
/// </summary>
public sealed class DevHarnessRouteTests
{
    private static readonly Type[] V2Controllers =
    {
        typeof(MixesController), typeof(SongsController), typeof(ChartsController),
        typeof(ChartScoresController), typeof(TierListsController), typeof(PlayersController),
        typeof(OfficialController), typeof(WeeklyChartsController)
    };

    /// <summary>Every GET route the v2 controllers register, as slash-separated templates.</summary>
    private static string[] RegisteredRoutes()
    {
        return V2Controllers.SelectMany(controller =>
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template;
            return controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
                .Select(get => Combine(prefix, get.Template));
        }).Distinct().ToArray();
    }

    private static string Combine(string? prefix, string? template)
    {
        if (string.IsNullOrEmpty(template)) return prefix ?? string.Empty;
        // A template starting with "/" escapes its controller's prefix, which is how a route lands
        // outside the collection it is declared on.
        if (template.StartsWith('/')) return template.TrimStart('/');

        return string.IsNullOrEmpty(prefix) ? template : $"{prefix}/{template}";
    }

    /// <summary>
    ///     Whether a requested path is served by a registered template. Same segment count, and each
    ///     registered segment either matches literally or is a parameter — so the harness asking for
    ///     <c>tier-lists/score-difficulty</c> is satisfied by <c>tier-lists/{listType}</c>, and
    ///     asking for a path with an extra segment is not satisfied by anything.
    /// </summary>
    private static bool Serves(string registered, string requested)
    {
        var left = registered.Split('/');
        var right = requested.Split('/');
        if (left.Length != right.Length) return false;

        return left.Zip(right).All(pair =>
            pair.First.StartsWith('{') || pair.Second.StartsWith('{')
            || string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryHarnessUrlResolvesToARegisteredRoute()
    {
        var registered = RegisteredRoutes();

        var missing = DevApiReader.RouteTemplates
            .Where(requested => !registered.Any(r => Serves(r, requested)))
            .ToArray();

        Assert.True(missing.Length == 0,
            $"The dev harness requests routes that are not registered: {string.Join(", ", missing)}. " +
            $"Registered: {string.Join(", ", registered.OrderBy(r => r))}");
    }

    /// <summary>
    ///     The matcher is only useful if it can say no — an extra segment is exactly the shape of the
    ///     bug this file exists for.
    /// </summary>
    [Fact]
    public void AnExtraSegmentIsNotServed()
    {
        Assert.False(Serves("api/v2/chart-scoring-levels", "api/v2/chart-analysis/chart-scoring-levels"));
        Assert.True(Serves("api/v2/tier-lists/{listType}", "api/v2/tier-lists/score-difficulty"));
    }

    /// <summary>
    ///     Guards the guard. If the reflection ever stops finding routes the assertion above passes
    ///     vacuously and the whole test becomes decoration.
    /// </summary>
    [Fact]
    public void TheRouteScanFindsSomething()
    {
        Assert.NotEmpty(RegisteredRoutes());
        Assert.NotEmpty(DevApiReader.RouteTemplates);
    }
}
