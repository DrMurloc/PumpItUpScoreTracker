using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Live-value write-back ratchet (comment-box fix, 2026-10-18): a raw <c>&lt;input&gt;</c> or
///     <c>&lt;textarea&gt;</c> that the circuit hears on every keystroke binds with <c>@bind</c>,
///     never <c>value="@x"</c> beside a hand-written <c>@oninput</c>.
///     <para>
///         Only <c>@bind</c> marks the input handler as updating the <c>value</c> attribute, which
///         is what lets the renderer patch its last render to the text the browser already has
///         before diffing. The hand-rolled pair has no such mark, so every keystroke's render
///         writes that keystroke's text back into the field — one round trip late, over whatever
///         was typed since. On a real connection the chart comment box lost keys and split what
///         survived between two copies of the text; on localhost the stale value IS the current
///         one, so nothing ever looked wrong. A range slider fails the same way, which is UX rule
///         12 (docs/UX-GUIDELINES.md). MudBlazor fields are not scanned: their own focus guard
///         already refuses the write-back.
///     </para>
///     <para>
///         The allowlist is the debt when the rule landed. Counts may only go DOWN.
///     </para>
/// </summary>
public sealed class LiveValueWriteBackTests
{
    private static readonly string[] ScannedFolders = { "Pages", "Components", "Shared" };

    private static readonly Regex HandRolledValue = new(@"\svalue\s*=\s*""?@", RegexOptions.Compiled);

    // Baseline captured 2026-10-18. Shrink-only.
    private static readonly IReadOnlyDictionary<string, int> Allowance = new Dictionary<string, int>
    {
        // The level slider: the rule-12 shape on a range input. Its fix is the RangeSlider
        // pattern (commit on change, paint the live readout in script), not @bind.
        ["Pages/Tools/LifeCalculator.razor"] = 1,
    };

    [Fact]
    public void LiveInputsNeverWriteTheirOwnValueBack()
    {
        var webRoot = Path.Combine(FindSolutionRoot(), "ScoreTracker");
        var found = new Dictionary<string, List<int>>();

        foreach (var file in ScannedFolders
                     .Select(f => Path.Combine(webRoot, f))
                     .Where(Directory.Exists)
                     .SelectMany(dir => Directory.EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories)))
        {
            var text = File.ReadAllText(file);
            foreach (var (tag, line) in OpenTags(text, "<input").Concat(OpenTags(text, "<textarea")))
            {
                if (!tag.Contains("@oninput", StringComparison.Ordinal)) continue;
                if (!HandRolledValue.IsMatch(tag)) continue;

                var relative = Path.GetRelativePath(webRoot, file).Replace('\\', '/');
                if (!found.TryGetValue(relative, out var lines)) found[relative] = lines = new List<int>();
                lines.Add(line);
            }
        }

        var violations = new List<string>();
        foreach (var (file, lines) in found)
        {
            var allowed = Allowance.GetValueOrDefault(file);
            if (lines.Count <= allowed) continue;
            violations.Add($"{file}:{string.Join(",", lines)}: an input the circuit hears on every keystroke " +
                           "binds with @bind=\"x\" @bind:event=\"oninput\" (a slider: the RangeSlider pattern), " +
                           "not value=\"@x\" beside @oninput — the hand-rolled pair writes each keystroke's " +
                           "value back one round trip late and eats whatever was typed since");
        }

        foreach (var (file, allowed) in Allowance)
        {
            var now = found.GetValueOrDefault(file)?.Count ?? 0;
            if (now < allowed)
                violations.Add($"{file}: down to {now} from an allowance of {allowed} — lower its entry " +
                               "(or remove it) so the debt cannot creep back");
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     Each open tag of the given element with the line it starts on. The tag ends at the first
    ///     <c>&gt;</c> outside a quoted attribute value — values legitimately carry <c>&gt;</c>
    ///     inside quotes (lambdas), so a bare IndexOf would cut tags short.
    /// </summary>
    private static IEnumerable<(string Tag, int Line)> OpenTags(string text, string open)
    {
        var searchFrom = 0;
        while (true)
        {
            var start = text.IndexOf(open, searchFrom, StringComparison.Ordinal);
            if (start < 0) yield break;

            var end = start + open.Length;
            // "<inputs" or "<input-group" are other elements, not this one.
            if (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '>' && text[end] != '/')
            {
                searchFrom = end;
                continue;
            }

            var inQuotes = false;
            while (end < text.Length && (inQuotes || text[end] != '>'))
            {
                if (text[end] == '"') inQuotes = !inQuotes;
                end++;
            }

            var line = text.AsSpan(0, start).Count('\n') + 1;
            yield return (text[start..(end < text.Length ? end + 1 : end)], line);
            searchFrom = end;
        }
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
