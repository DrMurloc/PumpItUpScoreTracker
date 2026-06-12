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
    private static readonly Assembly SharedKernelAssembly = typeof(Domain.ValueTypes.Name).Assembly;

    private static string[] ReferencedNames(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();
    }

    [Fact]
    public void SharedKernelReferencesNothingButMediatR()
    {
        var unexpected = ReferencedNames(SharedKernelAssembly)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                        && n != "netstandard" && n != "mscorlib"
                        && n != "MediatR" && n != "MediatR.Contracts")
            .ToArray();

        Assert.True(unexpected.Length == 0,
            $"ScoreTracker.SharedKernel must stay dependency-free (MediatR carve-out only), found: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void DomainReferencesOnlyItsAllowlistedPackages()
    {
        var allowed = new[]
        {
            "MediatR", "MediatR.Contracts", "Microsoft.Extensions.Logging.Abstractions",
            "ScoreTracker.SharedKernel"
        };
        var unexpected = ReferencedNames(DomainAssembly)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                        && n != "netstandard" && n != "mscorlib"
                        && !allowed.Contains(n))
            .ToArray();

        Assert.True(unexpected.Length == 0,
            $"ScoreTracker.Domain gained dependencies outside its allowlist: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void DomainReferencesNoProjectExceptSharedKernel()
    {
        var projectRefs = ReferencedNames(DomainAssembly)
            .Where(n => n.StartsWith("ScoreTracker", StringComparison.Ordinal)
                        && n != "ScoreTracker.SharedKernel")
            .ToArray();

        Assert.True(projectRefs.Length == 0,
            $"ScoreTracker.Domain may reference only SharedKernel, found: {string.Join(", ", projectRefs)}");
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
