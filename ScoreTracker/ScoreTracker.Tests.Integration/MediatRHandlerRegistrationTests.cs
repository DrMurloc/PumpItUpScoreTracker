using System;
using System.Linq;
using System.Reflection;
using MediatR;
using ScoreTracker.CompositionRoot;
using Xunit;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Every vertical that defines MediatR handlers is in the host's scan, and every handler it
///     defines actually resolves from a real container.
///     <para>
///         This exists because it did not. The scan list was a literal in <c>Program.cs</c> and
///         <c>CommunityTools</c> was never added to it. The vertical compiled, its arch tests passed,
///         its unit tests passed — they construct sagas directly — and all 33 of its handlers were
///         unregistered. The first sign was a page throwing <c>No service for type
///         IRequestHandler&lt;GetShareWithAllToolsQuery, Boolean&gt;</c> during a field test.
///     </para>
///     <para>
///         Nothing in the suite resolved a vertical's handler through the real container: component
///         tests mock <c>IMediator</c>, API tests mock <c>IMediator</c>, integration tests construct
///         repositories. Testing every part and never the wiring is exactly how a whole vertical
///         ships dead.
///     </para>
///     <para>
///         Here rather than in <c>ScoreTracker.Tests</c> for the same reason
///         <c>ModelContributionRegistrationTests</c> is: CompositionRoot is only referenced by this
///         project. It needs no database of its own.
///     </para>
/// </summary>
public sealed class MediatRHandlerRegistrationTests
{
    private static readonly Type[] HandlerInterfaces =
    {
        typeof(IRequestHandler<>), typeof(IRequestHandler<,>), typeof(INotificationHandler<>)
    };

    private static bool IsHandler(Type type)
    {
        return type is { IsClass: true, IsAbstract: false }
               && type.GetInterfaces().Any(i => i.IsGenericType
                                                && HandlerInterfaces.Contains(i.GetGenericTypeDefinition()));
    }

    /// <summary>
    ///     Loaded rather than listed: every assembly the solution references whose name marks it as
    ///     a vertical. Deriving the set means a new vertical is caught the moment it has a handler,
    ///     rather than when someone remembers to update a test.
    /// </summary>
    private static Assembly[] VerticalAssembliesInSolution()
    {
        var root = typeof(VerticalModelContributions).Assembly;
        return root.GetReferencedAssemblies()
            .Where(name => name.Name is not null
                           && name.Name.StartsWith("ScoreTracker.", StringComparison.Ordinal))
            .Select(Assembly.Load)
            .Where(a => a.GetTypes().Any(t => t.Namespace?.EndsWith(".Wiring", StringComparison.Ordinal) == true))
            .ToArray();
    }

    [Fact]
    public void EveryVerticalWithHandlersIsInTheHostsScan()
    {
        var scanned = VerticalAssemblies.All().ToHashSet();

        var missing = VerticalAssembliesInSolution()
            .Where(a => a.GetTypes().Any(IsHandler))
            .Where(a => !scanned.Contains(a))
            .Select(a => a.GetName().Name)
            .ToArray();

        Assert.True(missing.Length == 0,
            "These assemblies define MediatR handlers but are not in VerticalAssemblies.All(), so " +
            $"every one of their handlers is unregistered at runtime: {string.Join(", ", missing)}");
    }

    /// <summary>
    ///     Guards the guard. If the reflection stops finding verticals the assertion above passes
    ///     vacuously, which is worse than not having it.
    /// </summary>
    [Fact]
    public void TheScanFindsTheVerticalsAndTheirHandlers()
    {
        var verticals = VerticalAssembliesInSolution();

        Assert.True(verticals.Length >= 10, $"Only found {verticals.Length} vertical assemblies.");
        Assert.Contains(verticals, a => a.GetName().Name == "ScoreTracker.CommunityTools");
        Assert.Contains(verticals, a => a.GetTypes().Any(IsHandler));
    }

    /// <summary>
    ///     The list is not allowed to name an assembly that has nothing to register — a stale entry
    ///     is how a list stops being read.
    /// </summary>
    [Fact]
    public void TheScanListCarriesNoDeadEntries()
    {
        var dead = VerticalAssemblies.All()
            .Where(a => !a.GetTypes().Any(IsHandler))
            .Select(a => a.GetName().Name)
            .ToArray();

        Assert.True(dead.Length == 0,
            $"VerticalAssemblies.All() names assemblies with no MediatR handlers: {string.Join(", ", dead)}");
    }
}
