using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Structural ratchet for the shipped stylesheets (2026-08-06).
///     <para>
///         A single missing <c>}</c> does not fail a build, does not fail a page, and does not
///         look like anything in review — it silently swallows <b>every rule after it</b> into
///         the unterminated block, which the browser then discards. It was found when
///         <c>.rvl-absent</c> shipped without its closing brace and took the Community Tools
///         secret block with it; nobody noticed until a new page landed further down the file
///         and rendered as unstyled markup.
///     </para>
///     <para>
///         The same applies to an unterminated comment: <c>/*</c> without <c>*/</c> eats the
///         rest of the file. Both are invisible until something downstream breaks, which is
///         exactly what a ratchet is for.
///     </para>
/// </summary>
public sealed class StylesheetStructureTests
{
    [Fact]
    public void EveryShippedStylesheetIsStructurallyBalanced()
    {
        var failures = new List<string>();

        foreach (var file in Stylesheets())
        {
            var relative = Path.GetRelativePath(WebProjectRoot(), file).Replace('\\', '/');
            var (depth, unterminatedComment, openedAt, strayClose) = Scan(File.ReadAllText(file));

            if (unterminatedComment)
                failures.Add($"{relative}: a comment is never closed — every rule after it is dead.");
            if (strayClose > 0)
                failures.Add($"{relative}: {strayClose} closing brace(s) with nothing open, first at line {strayClose}.");
            if (depth > 0)
                failures.Add($"{relative}: {depth} unclosed brace(s), opened at line(s) " +
                             $"{string.Join(", ", openedAt.Take(5))} — every rule after that point is dead.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    ///     Brace depth outside comments and strings. A naive character count is not enough:
    ///     braces and comment markers legitimately appear inside quoted values (content:, url(),
    ///     font names), and counting those produces a false alarm that trains people to ignore
    ///     this test.
    /// </summary>
    private static (int Depth, bool UnterminatedComment, List<int> OpenedAt, int StrayClose) Scan(string css)
    {
        var depth = 0;
        var line = 1;
        var strayClose = 0;
        var openedAt = new List<int>();
        var inComment = false;
        var quote = '\0';

        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '\n') line++;

            if (inComment)
            {
                if (c == '*' && i + 1 < css.Length && css[i + 1] == '/')
                {
                    inComment = false;
                    i++;
                }

                continue;
            }

            if (quote != '\0')
            {
                if (c == '\\') i++;
                else if (c == quote) quote = '\0';
                continue;
            }

            if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                inComment = true;
                i++;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '{')
            {
                depth++;
                openedAt.Add(line);
            }
            else if (c == '}')
            {
                if (depth == 0) strayClose = line;
                else
                {
                    depth--;
                    openedAt.RemoveAt(openedAt.Count - 1);
                }
            }
        }

        return (depth, inComment, openedAt, strayClose);
    }

    private static IEnumerable<string> Stylesheets()
    {
        var css = Path.Combine(WebProjectRoot(), "wwwroot", "css");
        return Directory.Exists(css)
            ? Directory.EnumerateFiles(css, "*.css", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
            : Enumerable.Empty<string>();
    }

    private static string WebProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("ScoreTracker.sln not found above test bin directory");
        return Path.Combine(dir.FullName, "ScoreTracker");
    }
}
