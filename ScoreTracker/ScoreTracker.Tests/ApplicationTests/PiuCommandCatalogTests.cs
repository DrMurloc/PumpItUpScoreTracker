using System.Linq;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Pins the /piu command tree's declared shape. Choice lists: static-initialization order
///     can silently null a choice field captured by an eager initializer, which registers on
///     Discord as an option with no dropdown, so every declared list must survive with its
///     entries. Option kinds: the client enforces them before the handler runs, so a numeric
///     option declared with the wrong kind rejects valid input at the prompt.
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

    [Fact]
    public void CalcTakesCaloriesAsADecimalAndEveryJudgmentCountAsAnInteger()
    {
        // The result screen's kcal readout carries a fraction, so the option must be the
        // decimal kind — declared Integer, the client refuses "12.5" outright.
        var calc = PiuCommandCatalog.Commands.Single().SubCommands.Single(s => s.Name == "calc");

        var calories = calc.Options.Single(o => o.Name == "calories");
        Assert.Equal(BotCommandOptionType.Number, calories.Type);
        Assert.False(calories.Required);
        Assert.Equal(0, calories.MinValue);
        Assert.All(calc.Options.Where(o => o.Name != "calories"),
            o => Assert.Equal(BotCommandOptionType.Integer, o.Type));
    }
}
