using System;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The URL collector's total IS the progress bar's total, so it must match the renderer's
///     own workload: every art slot, de-duplicated, nulls dropped, the header bubble included.
/// </summary>
public sealed class ShareCardArtTests
{
    [Fact]
    public void CollectsEveryDistinctArtUrlOnTheCard()
    {
        var shared = "https://img.test/shared-jacket.png";
        var card = new TierListShareCard("t", "s", "st", "#000000", "#000000", "#000000", "#000000", "#000000",
            "https://example.test", "https://img.test/header-bubble.png",
            new[]
            {
                new TierListShareCard.Row("Hard", "#FB8C00", new[]
                {
                    new TierListShareCard.Tile(shared, "https://img.test/aa.png", "https://img.test/sg.png", null,
                        ExpectedGradeUrl: "https://img.test/aaa.png"),
                    new TierListShareCard.Tile(shared, null, null, null,
                        BubbleUrl: "https://img.test/s20.png")
                })
            });

        var urls = ShareCardArt.CollectUrls(card);

        Assert.Equal(new[]
        {
            shared, "https://img.test/aa.png", "https://img.test/sg.png", "https://img.test/aaa.png",
            "https://img.test/s20.png", "https://img.test/header-bubble.png"
        }, urls);
    }

    [Fact]
    public void ACardWithNoArtCollectsNothing()
    {
        var card = new TierListShareCard("t", "s", "st", "#000000", "#000000", "#000000", "#000000", "#000000",
            "https://example.test", null, Array.Empty<TierListShareCard.Row>());

        Assert.Empty(ShareCardArt.CollectUrls(card));
    }
}
