using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Architecture ratchets (ADR-001 D2): each rule pins a boundary that is already true.
///     Rules are only ever added, never removed — a failure here means a layer boundary
///     regressed, not that the test needs updating.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.Models.Chart).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.Commands.CreateUserCommand).Assembly;

    private static string[] ReferencedNames(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();
    }

    [Fact]
    public void DomainReferencesOnlyItsAllowlistedPackages()
    {
        var allowed = new[] { "MediatR", "MediatR.Contracts", "Microsoft.Extensions.Logging.Abstractions" };
        var unexpected = ReferencedNames(DomainAssembly)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                        && n != "netstandard" && n != "mscorlib"
                        && !allowed.Contains(n))
            .ToArray();

        Assert.True(unexpected.Length == 0,
            $"ScoreTracker.Domain gained dependencies outside its allowlist: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void DomainReferencesNoOtherScoreTrackerProject()
    {
        var projectRefs = ReferencedNames(DomainAssembly)
            .Where(n => n.StartsWith("ScoreTracker", StringComparison.Ordinal))
            .ToArray();

        Assert.True(projectRefs.Length == 0,
            $"ScoreTracker.Domain must reference no other project, found: {string.Join(", ", projectRefs)}");
    }

    [Fact]
    public void ApplicationReferencesNoInfrastructureOrPresentationPackages()
    {
        var forbiddenPrefixes = new[]
        {
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Azure", "Discord",
            "SendGrid", "HtmlAgilityPack", "Hangfire", "MudBlazor", "Tesseract", "Swashbuckle"
        };
        var references = ReferencedNames(ApplicationAssembly);
        var violations = references
            .Where(n => forbiddenPrefixes.Any(p => n.StartsWith(p, StringComparison.Ordinal)))
            // full MassTransit is infrastructure; only the abstractions package is allowed
            .Concat(references.Where(n => n == "MassTransit"))
            .ToArray();

        Assert.True(violations.Length == 0,
            $"ScoreTracker.Application gained infrastructure/presentation dependencies: {string.Join(", ", violations)}");
    }
}
