using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     The tier list saves its view to UiSettings and validates what comes back against an
///     allowlist. A view offered by a button but missing from that list saves correctly and is
///     discarded on the way in — which reads as "the page forgot my choice" and points nowhere
///     near the array that dropped it. "Personalized" shipped that way: four buttons, three
///     that survived a reload.
/// </summary>
public sealed class ViewPersistenceTests
{
    // Relative to the folder holding ScoreTracker.sln, which is itself named ScoreTracker.
    private const string Page = @"ScoreTracker\Pages\TierLists\ChartSkills.razor";

    [Fact]
    public void EveryViewTheTierListOffersSurvivesAReload()
    {
        var source = File.ReadAllText(Path.Combine(FindSolutionRoot(), Page));

        var allowlist = Regex.Match(source, @"ValidGroupings\s*=\s*\{([^}]*)\}");
        Assert.True(allowlist.Success, "ValidGroupings not found — has it been renamed?");
        var valid = Regex.Matches(allowlist.Groups[1].Value, "\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        // Every literal the markup hands ChangeGrouping. The signed-out/signed-in pair on My
        // Scores is a ternary, so both arms get picked up by matching each string in the call.
        var offered = Regex.Matches(source, @"ChangeGrouping\(([^)]*)\)")
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\"")
                .Select(s => s.Groups[1].Value))
            .ToHashSet();
        Assert.NotEmpty(offered);

        var dropped = offered.Where(v => !valid.Contains(v)).OrderBy(v => v).ToArray();
        Assert.True(dropped.Length == 0,
            $"the tier list offers {string.Join(", ", dropped)} but ValidGroupings does not list " +
            "them, so choosing one saves and then reverts to Community on the next load");
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
