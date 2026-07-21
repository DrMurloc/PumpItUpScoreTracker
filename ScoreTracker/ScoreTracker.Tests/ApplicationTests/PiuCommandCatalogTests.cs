using System.Linq;
using ScoreTracker.Communities.Contracts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Pins the /piu command tree's choice lists: static-initialization order can silently
///     null a choice field captured by an eager initializer, which registers on Discord as
///     an option with no dropdown. Every declared choice list must survive with its
///     entries.
/// </summary>
public sealed class PiuCommandCatalogTests
{
    [Fact]
    public void EveryDeclaredChoiceListSurvivesWithItsEntries()
    {
        var root = PiuCommandCatalog.Commands.Single();
        var suggest = root.SubCommands.Single(s => s.Name == "suggest");
        var random = root.SubCommands.Single(s => s.Name == "random");
        var weekly = root.SubCommandGroups.Single().SubCommands.Single(s => s.Name == "weekly");

        Assert.Equal(4, suggest.Options.Single(o => o.Name == "goal").Choices!.Count);
        Assert.Equal(2, suggest.Options.Single(o => o.Name == "type").Choices!.Count);
        Assert.Equal(3, random.Options.Single(o => o.Name == "type").Choices!.Count);
        Assert.Equal(2, weekly.Options.Single(o => o.Name == "mix").Choices!.Count);
        Assert.Equal(9, weekly.Options.Single(o => o.Name == "language").Choices!.Count);
    }
}
