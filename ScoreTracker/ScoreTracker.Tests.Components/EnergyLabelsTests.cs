using System.Linq;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Energy select's copy (docs/design/pumbility-overhaul.md D51): the three options in order,
///     under keys that are not the judgement names' keys.
/// </summary>
public sealed class EnergyLabelsTests
{
    [Fact]
    public void TheOptionsAreOfferedSafeReadFirst()
    {
        Assert.Equal(new[] { Energy.Good, Energy.Great, Energy.TopOfMyGame }, EnergyLabels.Options);
    }

    [Fact]
    public void GoodAndGreatAreKeyedApartFromTheJudgementsWithTheSameName()
    {
        // "Good" and "Great" are the judgement names' resx keys; a key shared across two meanings
        // would print the judgement's translation for an energy level in Korean and Japanese. The
        // copy is still the plain word — only the key carries the register.
        var labels = EnergyLabels.Options.Select(EnergyLabels.Label).ToArray();

        Assert.Equal(new[] { "Energy: Good", "Energy: Great", "Top of my game" }, labels);
        Assert.DoesNotContain("Good", labels);
        Assert.DoesNotContain("Great", labels);
        Assert.All(EnergyLabels.Options, option => Assert.False(string.IsNullOrWhiteSpace(EnergyLabels.Hint(option))));
    }
}
