using ScoreTracker.ChartComments.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The owner-worded display rule (2026-08-24), as a table. Language decides, region never
///     does, and nobody is shown a translation of a comment written in their own language.
/// </summary>
public sealed class CommentDisplayResolutionTests
{
    private static readonly string[] FullSet = { "en-US", "es-ES", "fr-FR", "pt-BR" };

    [Theory]
    // A Mexican reader and a peninsular comment share a language: the original, always.
    [InlineData("es-MX", "es")]
    [InlineData("es-ES", "es")]
    [InlineData("en-ZW", "en")]
    [InlineData("ko-KR", "ko")]
    public void OwnLanguageMeansTheOriginalWhateverTheRegion(string reader, string source)
    {
        var resolution = CommentDisplayResolution.Resolve(reader, null, source, FullSet, false);

        Assert.Null(resolution.RenderingLocale);
        Assert.False(resolution.Pending);
    }

    [Fact]
    public void APickIsTotalAndReadsEvenYourOwnLanguageComments()
    {
        // "Read in English" from a Spanish reader means everything reads English — the pick
        // substitutes for the reader, it does not negotiate with them (owner, field test).
        Assert.Equal("en-US",
            CommentDisplayResolution.Resolve("es-MX", "en-US", "es", FullSet, false).RenderingLocale);
    }

    [Fact]
    public void ACommentAlreadyInThePickedLanguageIsTheOriginalUnbadged()
    {
        // "Read in español" over a Spanish comment: the original IS the Spanish asked for.
        // Renderings into a comment's own language never exist, and nothing falls back to the
        // reader's locale — that fallback is how a Spanish pick once showed Spanish in English.
        var resolution = CommentDisplayResolution.Resolve("en-US", "es-ES", "es", FullSet, false);

        Assert.Null(resolution.RenderingLocale);
        Assert.False(resolution.Pending);
    }

    [Fact]
    public void APickedLanguageStillPendingShowsQueuedNotTheReadersMapping()
    {
        // Reader is English, picked Spanish, comment is Korean and untranslated: the promise is
        // Spanish-on-its-way, not a quiet fallback to the English rendering path.
        var resolution = CommentDisplayResolution.Resolve("en-US", "es-ES", null,
            System.Array.Empty<string>(), true);

        Assert.Null(resolution.RenderingLocale);
        Assert.True(resolution.Pending);
    }

    [Theory]
    // Mapping is by language, not region: es-MX lands on the es-ES rendering, Murloc on the pivot.
    [InlineData("es-MX", "es-ES")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("en-US", "en-US")]
    [InlineData("en-ZW", "en-US")]
    [InlineData("pt-BR", "pt-BR")]
    public void AForeignCommentMapsToTheReadersLanguage(string reader, string expected)
    {
        Assert.Equal(expected,
            CommentDisplayResolution.Resolve(reader, null, "ko", FullSet, false).RenderingLocale);
    }

    [Theory]
    // No rendering for the reader's language means the original — never forced English.
    [InlineData("ja-JP")]
    [InlineData("it-IT")]
    public void NoMatchMeansTheOriginalNotEnglish(string reader)
    {
        var resolution = CommentDisplayResolution.Resolve(reader, null, "ko", FullSet, false);

        Assert.Null(resolution.RenderingLocale);
        Assert.False(resolution.Pending);
    }

    [Fact]
    public void TheStoredPickIsHowAJapaneseReaderOptsIntoEnglish()
    {
        Assert.Equal("en-US",
            CommentDisplayResolution.Resolve("ja-JP", "en-US", "ko", FullSet, false).RenderingLocale);
    }

    [Fact]
    public void PendingBelongsOnlyToAReaderWhoseRenderingIsComing()
    {
        var none = System.Array.Empty<string>();

        // A mapped reader with nothing yet: queued and coming.
        Assert.True(CommentDisplayResolution.Resolve("es-MX", null, null, none, true).Pending);
        // A reader whose language never renders sees the original with no explanation owed...
        Assert.False(CommentDisplayResolution.Resolve("ja-JP", null, null, none, true).Pending);
        // ...unless their stored pick means one is coming for them after all.
        Assert.True(CommentDisplayResolution.Resolve("ja-JP", "en-US", null, none, true).Pending);
        // Not queued at all (a pre-pipeline comment): nothing is coming.
        Assert.False(CommentDisplayResolution.Resolve("es-MX", null, null, none, false).Pending);
    }

    [Fact]
    public void ACallerWithoutALocaleReadsOriginalsOnly()
    {
        var resolution = CommentDisplayResolution.Resolve(null, null, "ko", FullSet, true);

        Assert.Null(resolution.RenderingLocale);
        Assert.False(resolution.Pending);
    }
}
