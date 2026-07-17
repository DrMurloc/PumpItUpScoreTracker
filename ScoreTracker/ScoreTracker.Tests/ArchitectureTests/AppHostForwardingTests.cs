using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     The Aspire AppHost forwards user-secrets to the web app only for sections in its
///     forwardedSections allowlist — a section missing from that list works in production
///     (App Service settings reach the app directly) but silently gets nothing locally,
///     which reads as "config not populated" while the secrets sit right there in the
///     store. KeyVault and PiuGame both shipped with that hole. These ratchets scan the
///     sources on both sides so a new configuration section must make a conscious choice
///     in the same PR: forward it, or record here why it deliberately doesn't flow.
/// </summary>
public sealed class AppHostForwardingTests
{
    /// <summary>
    ///     Sections the app binds that deliberately do NOT flow through AppHost
    ///     user-secrets. Every entry carries its reason; grow this only with intent.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DeliberatelyNotForwarded =
        new Dictionary<string, string>
        {
            ["SQL"] = "the AppHost always injects the container connection string, so a pasted " +
                      "production string can never point local dev at prod",
            ["DevAuth"] = "the AppHost enables it explicitly via WithEnvironment — a local mode flag, " +
                          "not a secret",
            ["ProdSync"] = "the /Dev/Populate harness: hosts default in code and the API token is " +
                           "pasted on the page per use, never stored",
            ["PiuCenter"] = "public piucenter hosts with production defaults; nothing secret to forward"
        };

    private static readonly Regex GetSectionLiteral = new("GetSection\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex ForwardedArray = new(
        "forwardedSections\\s*=\\s*\\[(?<entries>[^\\]]*)\\]", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex QuotedString = new("\"([^\"]+)\"", RegexOptions.Compiled);

    [Fact]
    public void EverySectionTheAppBindsIsForwardedByTheAppHostOrConsciouslyExcluded()
    {
        var bound = BoundSections();
        var missing = bound
            .Where(s => !ForwardedSections().Contains(s, StringComparer.OrdinalIgnoreCase))
            .Where(s => !DeliberatelyNotForwarded.ContainsKey(s))
            .OrderBy(s => s)
            .ToArray();

        Assert.True(missing.Length == 0,
            "These configuration sections are bound by the app but neither forwarded by the AppHost " +
            "nor recorded as deliberately-not-forwarded — locally they will silently read empty even " +
            "with user-secrets set. Add each to forwardedSections in ScoreTracker.AppHost/AppHost.cs " +
            "(plus a docs/HOW-TO-RUN.md secrets row) or to DeliberatelyNotForwarded here with its " +
            $"reason: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryForwardedSectionIsActuallyBoundSomewhere()
    {
        var bound = BoundSections();
        var dead = ForwardedSections()
            .Where(s => !bound.Contains(s, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s)
            .ToArray();

        Assert.True(dead.Length == 0,
            "These AppHost forwardedSections entries match no GetSection binding in the app — a typo " +
            $"forwards nothing, silently: {string.Join(", ", dead)}");
    }

    [Fact]
    public void DeliberateExclusionsStayReal()
    {
        // An exclusion for a section nobody binds anymore is a stale reason — prune it.
        var bound = BoundSections();
        var stale = DeliberatelyNotForwarded.Keys
            .Where(s => !bound.Contains(s, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s)
            .ToArray();

        Assert.True(stale.Length == 0,
            $"These deliberately-not-forwarded entries match no GetSection binding anymore: {string.Join(", ", stale)}");
    }

    private static IReadOnlyCollection<string> ForwardedSections()
    {
        var appHost = Path.Combine(FindSolutionRoot(), "ScoreTracker.AppHost", "AppHost.cs");
        var match = ForwardedArray.Match(File.ReadAllText(appHost));
        Assert.True(match.Success,
            "Could not locate the forwardedSections array in ScoreTracker.AppHost/AppHost.cs — if the " +
            "secret flow-through moved, update this ratchet with it.");
        return QuotedString.Matches(match.Groups["entries"].Value)
            .Select(m => m.Groups[1].Value)
            .ToArray();
    }

    private static IReadOnlyCollection<string> BoundSections()
    {
        var root = FindSolutionRoot();
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            if (relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                relative.StartsWith("ScoreTracker.AppHost", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("ScoreTracker.Tests", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (Match match in GetSectionLiteral.Matches(File.ReadAllText(file)))
                sections.Add(match.Groups[1].Value.Split(':')[0]);
        }

        Assert.NotEmpty(sections);
        return sections;
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName
               ?? throw new InvalidOperationException("ScoreTracker.sln not found above test bin directory");
    }
}
