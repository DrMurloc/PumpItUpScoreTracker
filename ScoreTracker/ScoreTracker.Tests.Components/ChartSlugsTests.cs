using System;
using System.Collections.Generic;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ChartSlugsTests
{
    internal static Chart BuildChart(Guid? id = null, string song = "Baroque Virus - FULL SONG",
        MixEnum mix = MixEnum.Phoenix, int level = 20, ChartType type = ChartType.Double,
        LegacySlot? slot = null)
    {
        return new Chart(id ?? Guid.NewGuid(), MixEnum.XX,
            new Song(Name.From(song), SongType.FullSong, new Uri("https://example.invalid/x.png"),
                TimeSpan.FromMinutes(2), Name.From("msgoon"), null),
            type, level, mix, null, null, new HashSet<Skill>(), slot);
    }

    [Theory]
    [InlineData("Baroque Virus - FULL SONG", "baroque-virus-full-song")]
    [InlineData("Why Don't You Get Up and Dance, Man?", "why-dont-you-get-up-and-dance-man")]
    [InlineData("다시 만난 세계", "다시-만난-세계")] // unicode preserved — Korean titles stay Korean
    [InlineData("A__B  C", "a-b-c")]
    [InlineData("  Trailing -- Punct!!  ", "trailing-punct")]
    public void SongSlugsAreLowercaseHyphenatedAndUnicodePreserving(string song, string expected)
    {
        Assert.Equal(expected, ChartSlugs.SlugifySong(Name.From(song)));
    }

    [Theory]
    [InlineData(MixEnum.Phoenix, "phoenix")]
    [InlineData(MixEnum.Phoenix2, "phoenix-2")]
    [InlineData(MixEnum.XX, "xx")]
    [InlineData(MixEnum.ThirdObg, "3rd-obg")] // dots strip: "3rd O.B.G"
    public void MixSlugsComeFromTheMixDisplayName(MixEnum mix, string expected)
    {
        Assert.Equal(expected, ChartSlugs.MixSlug(mix));
    }

    [Fact]
    public void DifficultySlugIsTheLoweredDifficultyString()
    {
        Assert.Equal("d20", ChartSlugs.DifficultySlug(BuildChart()));
        Assert.Equal("s18", ChartSlugs.DifficultySlug(BuildChart(type: ChartType.Single, level: 18)));
    }

    [Fact]
    public void SlottedChartsSlugTheirSlotBecauseTheShorthandIsAmbiguous()
    {
        // Pre-Exceed, the same song carries Hard 6 AND Crazy 6 — "s6" names two charts.
        Assert.Equal("crazy-6",
            ChartSlugs.DifficultySlug(BuildChart(type: ChartType.Single, level: 6, slot: LegacySlot.Crazy)));
        Assert.Equal("another-hard-9",
            ChartSlugs.DifficultySlug(BuildChart(type: ChartType.Single, level: 9, slot: LegacySlot.AnotherHard)));
    }

    [Fact]
    public void UnslugifiableNamesFallBackToStableSlugs()
    {
        // "!" (Infinity) is all punctuation — Slugify drops every character.
        Assert.Equal("exclamation", ChartSlugs.SlugifySong(Name.From("!")));
        Assert.Equal("untitled", ChartSlugs.SlugifySong(Name.From("?!")));
    }

    [Fact]
    public void CanonicalPathMountsUnderChartsAndComposesMixSongAndDifficulty()
    {
        Assert.Equal("/Charts/phoenix/baroque-virus-full-song/d20", BuildChart().CanonicalPath());
    }
}
