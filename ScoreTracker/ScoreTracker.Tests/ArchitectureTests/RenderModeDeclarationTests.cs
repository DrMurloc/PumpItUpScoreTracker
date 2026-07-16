using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Every routable page declares its render mode explicitly. Since the router went
///     static (SSR ladder PR-3, docs/design/seo-friendly-site.md §7), a page without
///     @rendermode renders as static SSR — which must be a deliberate conversion, never
///     an accident: bUnit cannot see render modes, so a forgotten line would ship a page
///     whose buttons render and do nothing, with every fast suite green. Converted pages
///     move to StaticPages; the interactive set only ever shrinks.
/// </summary>
public sealed class RenderModeDeclarationTests
{
    /// <summary>Pages that render static SSR by design (the SSR ladder's PR-4 onward).</summary>
    private static readonly HashSet<string> StaticPages = new(StringComparer.Ordinal)
    {
        // The chart page: real HTML for crawlers, its circuit-needing sections islanded
        // (docs/design/chart-details-overhaul.md).
        "ChartDetails.razor"
    };

    [Fact]
    public void EveryRoutablePageDeclaresItsRenderMode()
    {
        var pagesRoot = Path.Combine(FindSolutionRoot(), "ScoreTracker", "Pages");
        var violations = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(pagesRoot, "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (!Regex.IsMatch(text, "^@page ", RegexOptions.Multiline)) continue;

            var path = Path.GetRelativePath(pagesRoot, file).Replace('\\', '/');
            seen.Add(path);
            var declares = Regex.IsMatch(text, "^@rendermode ", RegexOptions.Multiline);
            var listedStatic = StaticPages.Contains(path);
            if (!declares && !listedStatic)
                violations.Add(
                    $"{path}: routable page with no @rendermode — add '@rendermode RenderModes.Interactive', or list it in StaticPages if it converted to static SSR deliberately");
            else if (declares && listedStatic)
                violations.Add(
                    $"{path}: declares @rendermode but is listed in StaticPages — a page is exactly one of the two");
        }

        violations.AddRange(StaticPages.Where(p => !seen.Contains(p))
            .Select(p => $"{p}: listed in StaticPages but no longer exists — remove the entry"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
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
