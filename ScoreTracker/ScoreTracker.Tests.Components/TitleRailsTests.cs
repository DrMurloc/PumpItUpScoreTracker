using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The page's rail assembly. Built against the real title lists rather than fixtures: the
///     shapes that break layout — a rail whose rungs are not in requirement order, a capstone of
///     a different class from its rungs, a title with no formula behind it — only exist there.
/// </summary>
public sealed class TitleRailsTests
{
    private static readonly TitleRarityRecord NoRarity =
        new(new Dictionary<Name, int>(), 1000);

    private static IReadOnlyList<TitleSectionRows> Phoenix(ISet<Name>? completed = null)
    {
        var progress = PhoenixTitleList.BuildProgress(
            new Dictionary<Guid, Chart>(), Array.Empty<RecordedPhoenixScore>(),
            completed ?? new HashSet<Name>());
        return TitleRails.Build(progress, NoRarity);
    }

    private static IReadOnlyList<TitleSectionRows> Phoenix2(ISet<Name>? completed = null)
    {
        var progress = Phoenix2TitleList.BuildProgress(
            new Dictionary<Guid, Chart>(), Array.Empty<RecordedPhoenixScore>(),
            completed ?? new HashSet<Name>());
        return TitleRails.Build(progress, NoRarity);
    }

    private static TitleRailRow Rail(IReadOnlyList<TitleSectionRows> sections, string name)
    {
        return sections.SelectMany(s => s.Rails).Single(r => r.Name == (Name)name);
    }

    [Fact]
    public void EveryPhoenixTitleLandsInExactlyOneSection()
    {
        var sections = Phoenix();
        Assert.Equal(213, sections.Sum(s => s.Total));
    }

    [Fact]
    public void EveryPhoenixTwoTitleLandsInExactlyOneSection()
    {
        var sections = Phoenix2();
        Assert.Equal(272, sections.Sum(s => s.Total));
    }

    [Fact]
    public void SectionsRenderInDeclaredOrderAndEmptyOnesDoNotRender()
    {
        var sections = Phoenix().Select(s => s.Section).ToArray();
        Assert.Equal(sections.OrderBy(s => s), sections);
        // Phoenix has no PUMBILITY-only section and Phoenix 2 has no plates; neither mix
        // should ever render a section header with nothing under it.
        Assert.DoesNotContain(TitleSection.Plates, Phoenix2().Select(s => s.Section));
    }

    [Fact]
    public void ARailKeepsItsRungOrderRatherThanItsRequirementOrder()
    {
        // Advanced Lv.3 asks 39,000 and Lv.4 asks 15,000. Sorting by requirement would put
        // Lv.4 third; the rail has to read 1 through 10.
        var advanced = Rail(Phoenix(), "ADVANCED");
        Assert.Equal(Enumerable.Range(1, 10), advanced.Rungs.Select(r => r.Rung));
        Assert.Equal("Advanced Lv. 4", advanced.Rungs[3].Title.Name.ToString());
        Assert.True(advanced.Rungs[2].Title.CompletionRequired > advanced.Rungs[3].Title.CompletionRequired);
    }

    [Fact]
    public void ASkillRailEndsOnItsCapstone()
    {
        var bracket = Rail(Phoenix(), "BRACKET");
        Assert.Equal(11, bracket.Total);
        Assert.Equal("[BRACKET] EXPERT", bracket.Rungs[10].Title.Name.ToString());
    }

    [Fact]
    public void ATitleWithNoRequirementIsOfficialAndNeverPartlyDone()
    {
        // Plates, play counts and step-artist titles are counts of things we never see.
        var plates = Phoenix().Single(s => s.Section == TitleSection.Plates);
        Assert.All(plates.Rails, rail =>
        {
            Assert.True(rail.Official);
            Assert.All(rail.Rungs, rung =>
            {
                Assert.True(rung.Official);
                Assert.Equal(0, rung.Fraction);
                Assert.NotEqual(RungState.Active, rung.State);
            });
        });
    }

    [Fact]
    public void PhoenixLeavesSkillCapstonesToTheImportWhilePhoenixTwoComputesThem()
    {
        // The same title in the two mixes, backed by different classes — the marking has to
        // follow the model, not the name.
        Assert.True(Rail(Phoenix(), "BRACKET").Rungs[10].Official);
        Assert.False(Rail(Phoenix2(), "BRACKET").Rungs[10].Official);
    }

    [Fact]
    public void AnEarnedTitleReadsEarnedEvenThoughOnlyTheImportKnowsIt()
    {
        var sections = Phoenix(new HashSet<Name> { "GOLD MEMBER" });
        var membership = Rail(sections, "MEMBERSHIP");
        Assert.Equal(RungState.Earned, membership.Rungs[0].State);
        Assert.True(membership.Rungs[0].Official);
        Assert.Equal(1, membership.Earned);
    }

    [Fact]
    public void RarityReadsAsThePercentileOfPlayersWhoDoNotHoldIt()
    {
        var rarity = new TitleRarityRecord(
            new Dictionary<Name, int> { [(Name)"The Master"] = 8, [(Name)"Beginner"] = 1000 }, 1000);
        var progress = PhoenixTitleList.BuildProgress(
            new Dictionary<Guid, Chart>(), Array.Empty<RecordedPhoenixScore>(), new HashSet<Name>());
        var sections = TitleRails.Build(progress, rarity);
        var all = sections.SelectMany(s => s.Rails.SelectMany(r => r.Rungs).Concat(s.OneOffs)).ToArray();

        var master = all.Single(r => r.Title.Name == (Name)"The Master");
        var beginner = all.Single(r => r.Title.Name == (Name)"Beginner");

        Assert.Equal(0.008, master.Share, 3);
        Assert.Equal(RarityBand.Prism, master.Band);
        // Everyone holds Beginner, so it is the commonest thing on the page.
        Assert.Equal(RarityBand.Common, beginner.Band);
    }

    [Fact]
    public void ATitleNobodyHoldsIsTheRarestRatherThanAnError()
    {
        var sections = Phoenix();
        var all = sections.SelectMany(s => s.Rails.SelectMany(r => r.Rungs).Concat(s.OneOffs));
        Assert.All(all, rung =>
        {
            Assert.Equal(0, rung.Share);
            Assert.Equal(RarityBand.Prism, rung.Band);
        });
    }

    [Fact]
    public void PhoenixTwoPoolsAreThreeRailsUnderProgression()
    {
        var progression = Phoenix2().Single(s => s.Section == TitleSection.Progression);
        Assert.Equal(3, progression.Rails.Count);
        Assert.Equal(70, progression.Total);
    }

    [Fact]
    public void PhoenixTwoSkillTracksAllLandUnderOneSection()
    {
        var skill = Phoenix2().Single(s => s.Section == TitleSection.Skill);
        // Nine tracks of eleven, plus SPECIALIST which spans them all and rails to nothing.
        Assert.Equal(9, skill.Rails.Count);
        Assert.Single(skill.OneOffs);
        Assert.Equal("SPECIALIST", skill.OneOffs[0].Title.Name.ToString());
    }
}
