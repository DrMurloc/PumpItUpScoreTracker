using System.Linq;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Web.Accessors;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Pins the localized command tree against the real catalogues: descriptions carry
///     per-culture entries, translatable choice names localize, and untranslatable
///     names (mix brands, native language names) carry none.
/// </summary>
public sealed class PiuCommandCatalogLocalizationTests
{
    private static ResxLocalizedTextAccessor Accessor()
    {
        return new ResxLocalizedTextAccessor(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
            NullLoggerFactory.Instance));
    }

    [Fact]
    public void TheRootDescriptionCarriesEveryTranslatedCulture()
    {
        var root = PiuCommandCatalog.Localized(Accessor()).Single();

        Assert.NotNull(root.DescriptionLocalizations);
        Assert.Equal("PIU Scores 도구", root.DescriptionLocalizations!["ko-KR"]);
        Assert.Equal("Outils PIU Scores", root.DescriptionLocalizations["fr-FR"]);
        Assert.False(root.DescriptionLocalizations.ContainsKey("en-US")); // the default text itself
    }

    [Fact]
    public void GoalChoiceNamesLocalizeButMixBrandsAndNativeLanguageNamesDoNot()
    {
        var root = PiuCommandCatalog.Localized(Accessor()).Single();
        var suggest = root.SubCommands.Single(s => s.Name == "suggest");
        var goal = suggest.Options.Single(o => o.Name == "goal");
        var mix = suggest.Options.Single(o => o.Name == "mix");
        var weekly = root.SubCommandGroups.Single().SubCommands.Single(s => s.Name == "weekly");
        var language = weekly.Options.Single(o => o.Name == "language");

        Assert.Equal("칭호 사냥",
            goal.Choices!.Single(c => c.Value == "TitleHunt").NameLocalizations!["ko-KR"]);
        // "Phoenix" and "한국어" have no catalogue entries, so they localize to themselves
        // and the decorator omits them entirely.
        Assert.All(mix.Choices!, c => Assert.Null(c.NameLocalizations));
        Assert.All(language.Choices!, c => Assert.Null(c.NameLocalizations));
    }

    [Fact]
    public void EverySubcommandDescriptionIsTranslatedInKorean()
    {
        var root = PiuCommandCatalog.Localized(Accessor()).Single();
        var leaves = root.SubCommands
            .Concat(root.SubCommandGroups.SelectMany(g => g.SubCommands));

        Assert.All(leaves, s => Assert.True(
            s.DescriptionLocalizations != null && s.DescriptionLocalizations.ContainsKey("ko-KR"),
            $"'{s.Name}' description has no Korean entry"));
    }
}
