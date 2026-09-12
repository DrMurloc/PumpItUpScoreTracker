using System;
using System.Globalization;
using System.Threading;
using ScoreTracker.SharedKernel.Caching;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CacheKeysTests
{
    [Fact]
    public void MixKeyIsOwnerThenMixThenPartsJoinedByDoubleUnderscore()
    {
        var chartId = Guid.Parse("D57A8403-12D4-41CC-8040-6CBF828F8FC0");

        var key = CacheKeys.Mix("EFTierListRepository", MixEnum.Phoenix2, "Scores", chartId, 22);

        Assert.Equal("EFTierListRepository__Phoenix2__Scores__d57a8403-12d4-41cc-8040-6cbf828f8fc0__22", key);
    }

    [Fact]
    public void ViewerKeyCarriesItsMarkerSoItNeverCollidesWithAMixKeyOfTheSameParts()
    {
        var userId = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");

        var viewer = CacheKeys.Viewer("EFPlayerStatsRepository", MixEnum.Phoenix2, userId);
        var mix = CacheKeys.Mix("EFPlayerStatsRepository", MixEnum.Phoenix2, userId);

        Assert.Equal("EFPlayerStatsRepository__viewer__Phoenix2__e38954c4-b1b1-418a-93f6-c4b25c98b713", viewer);
        Assert.NotEqual(mix, viewer);
    }

    [Fact]
    public void NumericPartsFormatInvariantlyWhateverTheThreadCulture()
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var key = CacheKeys.Mix("CohortScoreProvider", MixEnum.Phoenix, ChartType.Single, 22.5);

            Assert.Equal("CohortScoreProvider__Phoenix__Single__22.5", key);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }

    [Fact]
    public void ANullPartIsAnEmptySegmentRatherThanAMissingOne()
    {
        var key = CacheKeys.Mix("PumbilityFoldersHandler", MixEnum.Phoenix2, null, "community");

        Assert.Equal("PumbilityFoldersHandler__Phoenix2____community", key);
    }

    [Fact]
    public void AnOwnerIsRequired()
    {
        Assert.Throws<ArgumentException>(() => CacheKeys.Mix(" ", MixEnum.Phoenix2));
    }
}
