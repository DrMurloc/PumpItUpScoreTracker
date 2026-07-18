using System.Collections.Generic;
using System.Linq;
using Discord;
using ScoreTracker.Data.Clients;
using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DiscordCommandTranslatorTests
{
    private static BotCommandDefinition SampleTree()
    {
        return new BotCommandDefinition("piu", "PIU Scores",
            new[]
            {
                new BotSubCommand("chart", "Find a chart",
                    new[]
                    {
                        new BotCommandOption("name", "Song name", BotCommandOptionType.String, Required: true,
                            Autocomplete: true),
                        new BotCommandOption("mix", "Which mix", BotCommandOptionType.String,
                            Choices: new[] { new BotOptionChoice("Phoenix", "Phoenix") })
                    }),
                new BotSubCommand("random", "Draw charts",
                    new[]
                    {
                        new BotCommandOption("count", "How many", BotCommandOptionType.Integer, MinValue: 1, MaxValue: 10)
                    }),
                new BotSubCommand("suggest", "Personal picks", new BotCommandOption[0], Ephemeral: true)
            },
            new[]
            {
                new BotSubCommandGroup("register", "Register a feed",
                    new[]
                    {
                        new BotSubCommand("weekly", "Weekly charts",
                            new[]
                            {
                                new BotCommandOption("mix", "Which mix", BotCommandOptionType.String, Required: true,
                                    Choices: new[]
                                    {
                                        new BotOptionChoice("Phoenix", "Phoenix"),
                                        new BotOptionChoice("Phoenix 2", "Phoenix2")
                                    })
                            }, Ephemeral: true)
                    })
            });
    }

    [Fact]
    public void ToPropertiesBuildsTheTopLevelCommand()
    {
        var props = DiscordCommandTranslator.ToProperties(SampleTree());

        Assert.Equal("piu", props.Name.Value);
        var top = props.Options.Value;
        Assert.Contains(top, o => o.Name == "chart" && o.Type == ApplicationCommandOptionType.SubCommand);
        Assert.Contains(top, o => o.Name == "register" && o.Type == ApplicationCommandOptionType.SubCommandGroup);
    }

    [Fact]
    public void ToPropertiesMapsOptionKindsRequirednessAndBounds()
    {
        var props = DiscordCommandTranslator.ToProperties(SampleTree());
        var chart = props.Options.Value.Single(o => o.Name == "chart");
        var name = chart.Options.Single(o => o.Name == "name");
        var random = props.Options.Value.Single(o => o.Name == "random");
        var count = random.Options.Single(o => o.Name == "count");

        Assert.Equal(ApplicationCommandOptionType.String, name.Type);
        Assert.True(name.IsRequired);
        Assert.True(name.IsAutocomplete);
        Assert.Equal(ApplicationCommandOptionType.Integer, count.Type);
        Assert.Equal(1, count.MinValue);
        Assert.Equal(10, count.MaxValue);
    }

    [Fact]
    public void ToPropertiesCarriesChoicesIntoTheGroupedSubcommand()
    {
        var props = DiscordCommandTranslator.ToProperties(SampleTree());
        var register = props.Options.Value.Single(o => o.Name == "register");
        var weekly = register.Options.Single(o => o.Name == "weekly");
        var mix = weekly.Options.Single(o => o.Name == "mix");

        Assert.True(mix.IsRequired);
        Assert.NotNull(mix.Choices);
        Assert.Equal(2, mix.Choices.Count);
        Assert.Contains(mix.Choices, c => c.Name == "Phoenix 2" && (string)c.Value == "Phoenix2");
    }

    [Fact]
    public void DescriptionLocalizationsMapCultureCodesToDiscordLocales()
    {
        // ko-KR flattens to Discord's "ko"; en-ZW and es-MX have no carryable Discord
        // locale (es-419 fails Discord.Net's validation) and drop out.
        var tree = new BotCommandDefinition("piu", "PIU Scores tools",
            new[]
            {
                new BotSubCommand("chart", "Find a chart", new BotCommandOption[0],
                    DescriptionLocalizations: new Dictionary<string, string>
                    {
                        ["ko-KR"] = "채보 찾기",
                        ["es-MX"] = "Encuentra un chart",
                        ["en-ZW"] = "Frglnd chrglrt"
                    })
            },
            new BotSubCommandGroup[0],
            DescriptionLocalizations: new Dictionary<string, string> { ["es-ES"] = "Herramientas de PIU Scores" });

        var props = DiscordCommandTranslator.ToProperties(tree);

        Assert.Equal("Herramientas de PIU Scores", props.DescriptionLocalizations["es-ES"]);
        var chart = props.Options.Value.Single(o => o.Name == "chart");
        Assert.Equal("채보 찾기", chart.DescriptionLocalizations["ko"]);
        Assert.DoesNotContain(chart.DescriptionLocalizations, kv => kv.Value.Contains("Frglnd"));
        Assert.DoesNotContain(chart.DescriptionLocalizations, kv => kv.Value.Contains("Encuentra"));
    }

    [Fact]
    public void ChoiceNameLocalizationsRideIntoTheBuiltChoices()
    {
        var tree = new BotCommandDefinition("piu", "PIU Scores tools",
            new[]
            {
                new BotSubCommand("suggest", "Personal picks",
                    new[]
                    {
                        new BotCommandOption("goal", "What to work on", Choices: new[]
                        {
                            new BotOptionChoice("Title Hunt", "TitleHunt",
                                new Dictionary<string, string> { ["ja-JP"] = "タイトル狙い" })
                        })
                    })
            },
            new BotSubCommandGroup[0]);

        var props = DiscordCommandTranslator.ToProperties(tree);
        var goal = props.Options.Value.Single(o => o.Name == "suggest").Options.Single(o => o.Name == "goal");

        Assert.Equal("タイトル狙い", goal.Choices.Single().NameLocalizations["ja"]);
    }

    [Fact]
    public void IsEphemeralReadsTheSubcommandFlagByPath()
    {
        var tree = SampleTree();

        Assert.True(DiscordCommandTranslator.IsEphemeral(tree, new[] { "suggest" }));
        Assert.False(DiscordCommandTranslator.IsEphemeral(tree, new[] { "chart" }));
        Assert.True(DiscordCommandTranslator.IsEphemeral(tree, new[] { "register", "weekly" }));
        Assert.False(DiscordCommandTranslator.IsEphemeral(tree, new[] { "register", "nonexistent" }));
    }

    [Fact]
    public void ResolveSubCommandReturnsNullForAnUnknownPath()
    {
        var tree = SampleTree();

        Assert.Null(DiscordCommandTranslator.ResolveSubCommand(tree, new string[0]));
        Assert.Null(DiscordCommandTranslator.ResolveSubCommand(tree, new[] { "chart", "extra", "deep" }));
        Assert.NotNull(DiscordCommandTranslator.ResolveSubCommand(tree, new[] { "chart" }));
    }
}
