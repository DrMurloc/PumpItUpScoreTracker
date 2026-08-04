using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     The row-highlight utility set (docs/design/rivals.md §3.6) is applied OVER four row
///     layouts that share nothing, so the class names are the only thing tying markup to style.
///     A name that markup emits and the stylesheet never defines renders as plain markup: no
///     error, no warning, no failing test anywhere else — which is how `.is-community` shipped
///     styling nothing on the feed and roster families.
/// </summary>
public sealed class HighlightVocabularyTests
{
    private static string SiteCss() => File.ReadAllText(
        Path.Combine(FindSolutionRoot(), "ScoreTracker", "wwwroot", "css", "site.css"));

    [Theory]
    [InlineData("is-rival")]
    [InlineData("is-community")]
    [InlineData("is-both")]
    public void EveryHighlightStateHasARule(string className)
    {
        var css = SiteCss();

        Assert.Contains($".{className} {{", css);
    }

    /// <summary>
    ///     Each state must actually paint. A rule that exists but sets nothing visible would
    ///     satisfy the check above while still rendering an unmarked row.
    /// </summary>
    [Theory]
    [InlineData("is-rival")]
    [InlineData("is-community")]
    [InlineData("is-both")]
    public void EveryHighlightStateSetsABackground(string className)
    {
        var block = BlockFor(SiteCss(), $".{className} {{");

        Assert.Contains("background", block);
    }

    /// <summary>
    ///     "Red for rivals wherever community members are highlighted" is the rule, and a Blazor
    ///     parameter nobody passes is silently null — the dialog kept its rival parameter through
    ///     three call sites that only ever passed the community one, so the segmented row was
    ///     unreachable while every test stayed green. A caller that highlights one must highlight
    ///     both.
    /// </summary>
    [Fact]
    public void EveryBoardThatHighlightsClubmatesAlsoHighlightsRivals()
    {
        var root = Path.Combine(FindSolutionRoot(), "ScoreTracker");
        var offenders = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            // The declaring component is where the pair is defined, not passed.
            .Where(x => !x.File.EndsWith("LeaderboardDialog.razor", StringComparison.Ordinal))
            .Where(x => x.Text.Contains("CommunityUserIds=") && !x.Text.Contains("RivalUserIds="))
            .Select(x => Path.GetFileName(x.File))
            .ToArray();

        Assert.True(offenders.Length == 0,
            "These pass CommunityUserIds but never RivalUserIds, so a rival can never light up "
            + "there: " + string.Join(", ", offenders));
    }

    /// <summary>
    ///     The precedence ladder lives in exactly one place. Copies are how this broke twice: a
    ///     board written before rivals existed keeps a you/clubmate ternary, nothing references
    ///     the missing arm, and the rival state is simply unreachable there — no failing test, no
    ///     warning, just a row that never turns red. The parameter-passing check below cannot see
    ///     it, because a board that resolves membership internally passes no parameters at all.
    ///     Emitting "is-both" outside the reader means someone wrote the ladder again.
    /// </summary>
    [Fact]
    public void OnlyOneImplementationOfThePrecedenceLadder()
    {
        var root = Path.Combine(FindSolutionRoot(), "ScoreTracker");
        var offenders = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.Ordinal)
                        || f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith("CommunityGlowReader.cs", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("\"is-both\"", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "These decide the row class themselves instead of calling CommunityGlowReader.RowClass, "
            + "so they will drift out of the ladder: " + string.Join(", ", offenders));
    }

    /// <summary>
    ///     One geometry for every state: a tint plus a bar down each edge, never a ring. A ring
    ///     at row height reads as a box drawn around the row and fights the board's own grid, and
    ///     it makes the segmented row look like a different component instead of the same one
    ///     carrying two states. New boards copy an existing rule, so the old shape comes back
    ///     unless something says no.
    /// </summary>
    [Theory]
    [InlineData("is-rival")]
    [InlineData("is-community")]
    [InlineData("is-both")]
    public void HighlightStatesUseEdgeBarsRatherThanARing(string className)
    {
        var block = BlockFor(SiteCss(), $".{className} {{");

        Assert.Contains("inset 3px 0 0", block);
        Assert.Contains("inset -3px 0 0", block);
        Assert.DoesNotContain("inset 0 0 0", block);
    }

    private static string BlockFor(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{selector} is not defined in site.css");
        var end = css.IndexOf('}', start);
        return css[start..end];
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
