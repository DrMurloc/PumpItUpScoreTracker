using System;
using System.Linq;
using Microsoft.Extensions.Localization;
using Moq;
using ScoreTracker.Web;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Theming;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The session share card (D25): the tier-list card with rows as twenty-minute sections, the
///     session points in the corner the tier-list card prints PUMBILITY in, and the three list
///     boundaries forced off whatever the remembered options say.
/// </summary>
public sealed class MoMShareCardComposerTests
{
    private static readonly IStringLocalizer<App> Localizer = Build();

    private static IStringLocalizer<App> Build()
    {
        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
        return localizer.Object;
    }

    [Fact]
    public void RowsAreTheSessionsTwentyMinuteSectionsAndTheCornerPrintsPoints()
    {
        var view = MoMComponentData.Session();
        var palette = MixThemes.PaletteFor(MixEnum.Phoenix);

        var card = MoMShareCardComposer.Compose(view, ShareCardOptions.Default with { Pumbility = true }, palette, "2025-02-14", Localizer);

        Assert.Equal("March of Murlocs Winter 2025 — Doubles", card.Title);
        Assert.Contains("김재현 · 6,388 points · 3 charts · Phoenix · 2025-02-14", card.Subtitle);
        Assert.Equal("1st of 3", card.Stamp);
        Assert.EndsWith($"/MarchOfMurlocs/Session/{view.SessionId}", card.LinkUrl);
        Assert.Null(card.BubbleUrl);
        // Slam starts at 0:00, Adrenaline Blaster at ~44 min, Gargoyle at ~98 min: three rows.
        Assert.Equal(new[] { "0–20 min", "40–60 min", "80–100 min" }, card.Rows.Select(r => r.Name));
        Assert.All(card.Rows, r => Assert.Single(r.Tiles));
        var slam = card.Rows[0].Tiles[0];
        Assert.Equal("1,528", slam.CornerLabel);
        Assert.Equal(palette.Primary, slam.CornerHex);
        Assert.True(slam.CompactMarks);
        Assert.NotNull(slam.GradeUrl);
        Assert.Contains("difficulty", slam.BubbleUrl!);
    }

    [Fact]
    public void TheListBoundariesAreForcedOffWhateverWasRemembered()
    {
        var remembered = ShareCardOptions.Default with { BoundaryTodo = true, BoundaryOtherMixes = true, BoundaryTop50 = true, Pumbility = true, ExpectedGains = true };

        var forSession = MoMShareCardComposer.ForSession(remembered);
        var card = MoMShareCardComposer.Compose(MoMComponentData.Session(), remembered, MixThemes.PaletteFor(MixEnum.Phoenix), "2025-02-14", Localizer);

        Assert.False(forSession.BoundaryTodo);
        Assert.False(forSession.BoundaryOtherMixes);
        Assert.False(forSession.BoundaryTop50);
        Assert.False(forSession.ExpectedGains);
        Assert.True(forSession.Pumbility);
        Assert.True(forSession.BoundaryPass);
        Assert.NotNull(card.Legend);
        Assert.DoesNotContain(card.Legend!, e => e.Label.Contains("To Do") || e.Label.Contains("Top 50") || e.Label.Contains("other mixes"));
        Assert.All(card.Rows.SelectMany(r => r.Tiles), t => Assert.Null(t.ExpectedGradeUrl));
    }

    [Fact]
    public void WithoutThePointsSwitchTheCornerStaysEmptyAndTheSampleIsOneExampleRow()
    {
        var view = MoMComponentData.Session();
        var palette = MixThemes.PaletteFor(MixEnum.Phoenix);

        var card = MoMShareCardComposer.Compose(view, ShareCardOptions.Default, palette, "2025-02-14", Localizer);
        var sample = MoMShareCardComposer.Sample(view, ShareCardOptions.Default, palette, "2025-02-14", Localizer);

        Assert.All(card.Rows.SelectMany(r => r.Tiles), t => Assert.Null(t.CornerLabel));
        var row = Assert.Single(sample!.Rows);
        Assert.Equal("Example", row.Name);
        Assert.Equal(3, row.Tiles.Count);
        Assert.Equal("MarchOfMurlocs_Phoenix_Winter2025_Doubles_김재현_2025-02-14.png", MoMShareCardComposer.FileName(view, "2025-02-14"));
    }

    [Fact]
    public void ADraftIsStampedAsOne()
    {
        var card = MoMShareCardComposer.Compose(MoMComponentData.Session(draft: true), ShareCardOptions.Default,
            MixThemes.PaletteFor(MixEnum.Phoenix), "2025-02-14", Localizer);
        Assert.Equal("Draft", card.Stamp);
    }
}
