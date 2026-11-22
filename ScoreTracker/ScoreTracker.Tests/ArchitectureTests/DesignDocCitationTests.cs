using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Every design-doc citation resolves. The design docs are the record of why the code is the
///     way it is until a feature has a spec, and code cites them by path; a doc that moves or
///     splits leaves those paths pointing nowhere, with nothing to say so. This is how
///     <c>home-page-widgets.md</c> stayed cited for months after it became
///     <c>docs/design/HomePageWidgets/</c>.
///     <para>
///         Files only, no allowance: (1) every <c>docs/design/….md</c> path written in a C#, Razor,
///         CSS, JS, JSON, markdown or YAML file names a file that exists, and (2) every relative
///         <c>.md</c> link inside <c>docs/</c> lands on one. Section and decision anchors are not
///         checked.
///     </para>
/// </summary>
public sealed class DesignDocCitationTests
{
    private static readonly string[] TextExtensions =
        { ".cs", ".razor", ".cshtml", ".css", ".js", ".json", ".md", ".yml", ".yaml" };

    private static readonly Regex Citation = new(@"docs/design/[\w./-]+?\.md(?![\w-])", RegexOptions.Compiled);

    private static readonly Regex MarkdownLink = new(@"\]\(([^)\s]+?\.md)(?:#[^)\s]*)?\)", RegexOptions.Compiled);

    private static readonly Regex Scheme = new(@"^[a-zA-Z][a-zA-Z0-9+.-]*:", RegexOptions.Compiled);

    [Fact]
    public void EveryCitedDesignDocExists()
    {
        var cited = 0;
        var broken = new List<string>();
        foreach (var file in SourceTree.Files().Where(IsText))
        foreach (var path in Citations(SourceTree.ReadAllText(file)))
        {
            cited++;
            if (!File.Exists(SourceTree.FullPath(path))) broken.Add($"{file}: {path}");
        }

        Assert.True(cited > 0, "The scan found no design-doc citations at all, so it is reading nothing.");
        Assert.True(broken.Count == 0,
            "These design-doc paths name no file. Point each at the file that now holds what it cites:" +
            Environment.NewLine + string.Join(Environment.NewLine, broken.Distinct().OrderBy(b => b, StringComparer.Ordinal)));
    }

    [Fact]
    public void EveryRelativeMarkdownLinkInsideDocsResolves()
    {
        var linked = 0;
        var broken = new List<string>();
        foreach (var file in SourceTree.Files()
                     .Where(f => f.StartsWith("docs/", StringComparison.Ordinal)
                                 && f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
        {
            var folder = Path.GetDirectoryName(SourceTree.FullPath(file))!;
            foreach (var target in RelativeMarkdownLinks(SourceTree.ReadAllText(file)))
            {
                linked++;
                if (!File.Exists(Path.GetFullPath(Path.Combine(folder, target)))) broken.Add($"{file}: {target}");
            }
        }

        Assert.True(linked > 0, "The scan found no relative markdown links in docs/, so it is reading nothing.");
        Assert.True(broken.Count == 0,
            "These links inside docs/ land on no file:" +
            Environment.NewLine + string.Join(Environment.NewLine, broken.Distinct().OrderBy(b => b, StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData("see docs/design/rivals.md §2.4", "docs/design/rivals.md")]
    [InlineData("(docs/design/HomePageWidgets/README.md §2.2)", "docs/design/HomePageWidgets/README.md")]
    [InlineData("/// The rule (docs/design/seasons.md D27).", "docs/design/seasons.md")]
    [InlineData("[the doc](../../docs/design/daily-step.md#scope)", "docs/design/daily-step.md")]
    [InlineData("docs/design/step-chart-comments", null)]
    [InlineData("every docs/design/*.md file", null)]
    [InlineData("docs/DESIGN-SYSTEM.md", null)]
    public void TheCitationPatternFindsDesignDocPaths(string text, string? expected)
    {
        Assert.Equal(expected, Citations(text).SingleOrDefault());
    }

    [Theory]
    [InlineData("[ARCHITECTURE.md](../ARCHITECTURE.md)", "../ARCHITECTURE.md")]
    [InlineData("[§4](rise.md#4-the-capture-app)", "rise.md")]
    [InlineData("[PR](https://example.org/a.md)", null)]
    [InlineData("[home](/docs/README.md)", null)]
    [InlineData("[code](../ScoreTracker/Program.cs)", null)]
    public void TheLinkPatternFindsRelativeMarkdownLinks(string text, string? expected)
    {
        Assert.Equal(expected, RelativeMarkdownLinks(text).SingleOrDefault());
    }

    [Fact]
    public void TheWalkFindsAKnownFileAndSkipsBuildOutput()
    {
        var files = SourceTree.Files().ToHashSet(StringComparer.Ordinal);

        Assert.Contains("docs/design/rivals.md", files);
        Assert.Contains("ScoreTracker/ScoreTracker.Tests/ArchitectureTests/SourceTree.cs", files);
        Assert.DoesNotContain(files, f => SourceTree.IsSkipped(f));
    }

    [Theory]
    [InlineData("ScoreTracker/ScoreTracker.Tests/bin/Debug/x.dll", true)]
    [InlineData("ScoreTracker/ScoreTracker.Data/obj/project.assets.json", true)]
    [InlineData(".claude/worktrees/other/docs/x.md", true)]
    [InlineData(".git/HEAD", true)]
    [InlineData("docs/design/rivals.md", false)]
    [InlineData("ScoreTracker/ScoreTracker.Tests/ArchitectureTests/SourceTree.cs", false)]
    public void TheWalkJudgesFoldersOnTheRepoRelativePath(string relativePath, bool skipped)
    {
        Assert.Equal(skipped, SourceTree.IsSkipped(relativePath));
    }

    private static bool IsText(string file)
    {
        return TextExtensions.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Citations(string text)
    {
        return Citation.Matches(text).Select(m => m.Value);
    }

    private static IEnumerable<string> RelativeMarkdownLinks(string text)
    {
        return MarkdownLink.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Where(t => !Scheme.IsMatch(t) && !t.StartsWith('/'));
    }
}
