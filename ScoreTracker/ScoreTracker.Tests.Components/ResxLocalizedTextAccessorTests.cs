using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Accessors;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Pins the resx bridge the Discord composers localize through: real catalogue
///     lookups (via the Web project's satellite assemblies), the English fallback for
///     unknown cultures and missing keys, and that the ambient culture is restored.
/// </summary>
public sealed class ResxLocalizedTextAccessorTests
{
    private static ResxLocalizedTextAccessor Accessor()
    {
        return new ResxLocalizedTextAccessor(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
            NullLoggerFactory.Instance));
    }

    [Fact]
    public void KoreanLookupReadsTheKoreanCatalogue()
    {
        Assert.Equal("주간 채보", Accessor().Get("ko-KR", "Weekly Charts"));
    }

    [Fact]
    public void CultureCodesResolveCaseInsensitively()
    {
        Assert.Equal("주간 채보", Accessor().Get("KO-kr", "Weekly Charts"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("xx-YY")]
    public void UnknownOrMissingCultureFallsBackToEnglish(string? culture)
    {
        Assert.Equal("Weekly Charts", Accessor().Get(culture, "Weekly Charts"));
    }

    [Fact]
    public void MissingKeyFallsBackToTheKeyItself()
    {
        Assert.Equal("Not A Real Key 123", Accessor().Get("ko-KR", "Not A Real Key 123"));
    }

    [Fact]
    public void FormatArgumentsSubstituteIntoTheLocalizedTemplate()
    {
        var text = Accessor().Get("ko-KR", "Phoenix Import Saving Progress", 1, 2, 3, 4);

        Assert.Contains("1/2", text);
        Assert.Contains("업로드", text);
    }

    [Fact]
    public void TheAmbientCultureIsRestoredAfterALookup()
    {
        var ui = CultureInfo.CurrentUICulture;
        var format = CultureInfo.CurrentCulture;

        Accessor().Get("ja-JP", "Weekly Charts");

        Assert.Equal(ui, CultureInfo.CurrentUICulture);
        Assert.Equal(format, CultureInfo.CurrentCulture);
    }

    [Fact]
    public void EveryCatalogueHasASupportedCulturesEntryAndViceVersa()
    {
        // The canonical list drives the request-localization setup, the picker, and the
        // Discord language choices — a new resx must land in it, and vice versa.
        var resxCodes = Directory
            .GetFiles(Path.Combine(FindWebProject(), "Resources"), "App.*.resx")
            .Select(f => Path.GetFileNameWithoutExtension(f)["App.".Length..])
            .OrderBy(c => c)
            .ToArray();

        Assert.Equal(resxCodes, SupportedCultures.All.Select(c => c.Code).OrderBy(c => c).ToArray());
    }

    private static string FindWebProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "ScoreTracker");
    }
}
