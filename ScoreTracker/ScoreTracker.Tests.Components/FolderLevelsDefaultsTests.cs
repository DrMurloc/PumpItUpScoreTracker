using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     What a freshly-dropped Folder Levels widget fills itself with. The rule is "around your
///     competitive level, both disciplines" — these pin what that means at each cell size
///     (docs/design/folder-level-progression.md §6).
/// </summary>
public sealed class FolderLevelsDefaultsTests
{
    private static string Name(FolderLevelsTarget t) => $"{t.Type.GetShortHand()}{t.Level}";

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(2, 4)]
    public void EverySupportedSizeHasACapacity(int columns, int rows)
    {
        Assert.True(FolderLevelsDefaults.CapacityFor(new SizePreset(columns, rows)) > 0);
    }

    [Fact]
    public void SuggestionsAlternateSinglesAndDoublesFromEachDisciplinesOwnLevel()
    {
        var picks = FolderLevelsDefaults.Suggest(6, 21.34, 19.87).Select(Name).ToArray();

        // Own level first, then upward, then down — each type walking its own competitive level.
        Assert.Equal(new[] { "S21", "D19", "S22", "D20", "S20", "D18" }, picks);
    }

    [Fact]
    public void ASingleFolderWidgetLeadsWithSingles()
    {
        Assert.Equal("S21", Name(Assert.Single(FolderLevelsDefaults.Suggest(1, 21.34, 19.87))));
    }

    [Fact]
    public void SuggestionsNeverLeaveTheLevelsATypeActuallyHas()
    {
        // Singles stop at 26 and doubles at 29, so a ceiling player's walk goes down, not past.
        var picks = FolderLevelsDefaults.Suggest(8, 26, 29);

        Assert.All(picks, p =>
        {
            var (min, max) = FolderLevels.Range(p.Type);
            Assert.InRange(p.Level, min, max);
        });
        Assert.Equal(8, picks.Count);
    }

    [Fact]
    public void ACellIsFilledEvenWhenOneDisciplineRunsOutOfLevels()
    {
        // A brand-new account sits at level 1 in both, where the walk can only climb.
        var picks = FolderLevelsDefaults.Suggest(8, 1, 1);

        Assert.Equal(8, picks.Count);
        Assert.Equal(8, picks.Select(Name).Distinct().Count());
    }

    [Fact]
    public void SuggestionsDoNotRepeatAFolder()
    {
        var picks = FolderLevelsDefaults.Suggest(8, 21.34, 19.87).Select(Name).ToArray();

        Assert.Equal(picks.Length, picks.Distinct().Count());
    }
}
