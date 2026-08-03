using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The browser-language mapping behind anonymous localization. Every catalogue we ship is a
///     specific culture and ASP.NET's request localization only falls back upward, so without
///     <see cref="SupportedCultures.ResolveClosest" /> a visitor sending "es" or "es-CL" lands on
///     English. These pin the table, and — the reason it exists as pure string work — that no
///     tag, however malformed, can throw on the request path.
/// </summary>
public sealed class SupportedCulturesTests
{
    /// <summary>
    ///     An exactly-supported tag is returned untouched. es-MX must never be re-regioned to
    ///     es-ES by the primary-subtag table sitting behind it.
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("es-MX")]
    [InlineData("es-ES")]
    [InlineData("pt-BR")]
    [InlineData("ko-KR")]
    [InlineData("ja-JP")]
    [InlineData("fr-FR")]
    [InlineData("it-IT")]
    [InlineData("en-ZW")]
    public void ExactlySupportedTagsResolveToThemselves(string tag)
    {
        Assert.Equal(tag, SupportedCultures.ResolveClosest(tag));
    }

    [Theory]
    [InlineData("EN-us", "en-US")]
    [InlineData("ko-kr", "ko-KR")]
    [InlineData("  ja-JP  ", "ja-JP")]
    public void MatchingIgnoresCasingAndSurroundingWhitespace(string tag, string expected)
    {
        Assert.Equal(expected, SupportedCultures.ResolveClosest(tag));
    }

    /// <summary>A bare language subtag picks the catalogue we translate that language into.</summary>
    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("es", "es-ES")]
    [InlineData("pt", "pt-BR")]
    [InlineData("ko", "ko-KR")]
    [InlineData("ja", "ja-JP")]
    [InlineData("fr", "fr-FR")]
    [InlineData("it", "it-IT")]
    public void BareLanguageSubtagsResolveToTheirCatalogue(string tag, string expected)
    {
        Assert.Equal(expected, SupportedCultures.ResolveClosest(tag));
    }

    /// <summary>
    ///     The case this was written for: a region we carry no catalogue for. Peru, Chile and
    ///     Argentina are a large share of the playerbase and none of their tags can match
    ///     es-MX or es-ES on their own.
    /// </summary>
    [Theory]
    [InlineData("es-CL", "es-ES")]
    [InlineData("es-PE", "es-ES")]
    [InlineData("es-AR", "es-ES")]
    [InlineData("es-419", "es-ES")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("fr-CA", "fr-FR")]
    [InlineData("en-GB", "en-US")]
    [InlineData("en-AU", "en-US")]
    [InlineData("ja-Latn-JP", "ja-JP")]
    public void UnlistedRegionsResolveToTheirLanguagesCatalogue(string tag, string expected)
    {
        Assert.Equal(expected, SupportedCultures.ResolveClosest(tag));
    }

    /// <summary>
    ///     Anything unplaceable returns null so the middleware's default culture applies
    ///     unchanged. Null — not a guess, and not an exception: this runs on every anonymous
    ///     request, against a header the browser controls.
    /// </summary>
    [Theory]
    [InlineData("zz-ZZ")]
    [InlineData("de")]
    [InlineData("zh-CN")]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("-")]
    [InlineData("---")]
    [InlineData("!!!")]
    [InlineData("1234")]
    [InlineData("a-very-long-nonsense-tag-that-is-not-a-language")]
    public void UnplaceableTagsResolveToNullWithoutThrowing(string? tag)
    {
        Assert.Null(SupportedCultures.ResolveClosest(tag));
    }

    /// <summary>
    ///     Murloc is a joke locale reachable only by asking for it exactly. Nothing may fall
    ///     back into it — an English-speaking visitor from Zimbabwe notwithstanding, "en" is
    ///     en-US.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("en-GB")]
    [InlineData("en-AU")]
    [InlineData("zw")]
    public void MurlocIsNeverAFallback(string tag)
    {
        Assert.NotEqual("en-ZW", SupportedCultures.ResolveClosest(tag));
    }

    /// <summary>
    ///     Guards the table against a typo: whatever it hands back must be a culture we
    ///     actually ship a catalogue for, or the visitor gets a culture with no resx behind it.
    /// </summary>
    [Fact]
    public void EveryResolvedCultureIsItselfSupported()
    {
        var probes = new[]
        {
            "en", "es", "pt", "ko", "ja", "fr", "it",
            "es-CL", "pt-PT", "fr-CA", "en-GB", "ja-Latn-JP", "es-419"
        };

        foreach (var probe in probes)
        {
            var resolved = SupportedCultures.ResolveClosest(probe);
            Assert.NotNull(resolved);
            Assert.True(SupportedCultures.IsSupported(resolved),
                $"'{probe}' resolved to '{resolved}', which is not a supported culture.");
        }
    }
}
