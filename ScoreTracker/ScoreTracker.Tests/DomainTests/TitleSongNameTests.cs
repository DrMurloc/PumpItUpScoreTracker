using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     A chart title finds its chart by exact (case-insensitive) song name, so a name that isn't the
///     catalog's spelling matches nothing and the title silently never completes — no error, no
///     progress, just a rung that stays at 0 forever. Thirteen Phoenix 2 titles shipped that way,
///     naming songs the way the official requirement text abbreviates them ("Phalanx" for
///     <c>Phalanx "RS2018 edit"</c>), which also made SPECIALIST unreachable since it needs all 90
///     skill titles.
///     <para>
///         These are the offline guards. Only a real catalog can prove a name resolves —
///         <c>ScoreTracker.ExplorationTests/Catalog/TitleChartResolutionProbeTests</c> does that
///         against a prod-synced database, and is the check to run after editing a title's song.
///     </para>
/// </summary>
public sealed class TitleSongNameTests
{
    // The names corrected when the mismatch was found: the catalog spelling on the left of the
    // arrow in each case is what the official title page abbreviates away. Pinned so an edit back
    // to the short form fails here instead of silently freezing the ladder.
    public static readonly TheoryData<string, string> CorrectedPhoenix2Songs = new()
    {
        { "[TWIST S] LV.7", "Love is a Danger Zone(Cranky Mix)" },
        { "[TWIST S] LV.9", "Love is a Danger Zone pt. 2" },
        { "[TWIST D] LV.9", "Love is a Danger Zone(Cranky Mix)" },
        { "[TWIST D] LV.10", "Love is a Danger Zone pt. 2" },
        { "[SLOW] LV.2", "Twist of Fate (feat. Ruriling)" },
        { "[SLOW] LV.7", "Twist of Fate (feat. Ruriling)" },
        { "[HALF] LV.7", "Utsushiyo No Kaze feat. Kana" },
        { "[BRACKET] LV.4", "Meteo5cience (GADGET mix)" },
        { "[BRACKET] LV.5", "Phalanx \"RS2018 edit\"" },
        { "[BRACKET] LV.6", "Meteo5cience (GADGET mix)" },
        { "[BRACKET] LV.9", "Phalanx \"RS2018 edit\"" },
        { "[ZERO] SINGLE BOSS BREAKER", "Love is a Danger Zone pt. 2" },
        { "[ZERO] DOUBLE BOSS BREAKER", "Love is a Danger Zone pt. 2" },
        { "[FIESTA2] SINGLE BOSS BREAKER", "Ignis Fatuus(DM Ashura Mix)" },
        { "[FIESTA2] DOUBLE BOSS BREAKER", "Ignis Fatuus(DM Ashura Mix)" }
    };

    // Phoenix 1 shipped the same abbreviation for the FIESTA2 boss, so those two titles had been
    // uncompletable since the list was written — the catalog probe found them the first time it ran.
    public static readonly TheoryData<string, string> CorrectedPhoenixSongs = new()
    {
        { "[FIESTA2] Single Boss breaker", "Ignis Fatuus(DM Ashura Mix)" },
        { "[FIESTA2] Double Boss breaker", "Ignis Fatuus(DM Ashura Mix)" }
    };

    [Theory]
    [MemberData(nameof(CorrectedPhoenix2Songs))]
    public void Phoenix2TitlesNameTheirSongTheWayTheCatalogSpellsIt(string titleName, string songName)
    {
        var title = Assert.IsAssignableFrom<ISpecificChartTitle>(Phoenix2TitleList.GetTitleByName(titleName));

        Assert.Equal(songName, (string)title.SongName);
    }

    [Theory]
    [MemberData(nameof(CorrectedPhoenixSongs))]
    public void PhoenixTitlesNameTheirSongTheWayTheCatalogSpellsIt(string titleName, string songName)
    {
        var title = Assert.IsAssignableFrom<ISpecificChartTitle>(PhoenixTitleList.GetTitleByName(titleName));

        Assert.Equal(songName, (string)title.SongName);
    }

    [Fact]
    public void SongsNamedByBothTitleListsAreSpelledIdentically()
    {
        // The two lists name many of the same songs, and Phoenix 1's spellings are proven — its
        // titles complete in production. So a Phoenix 2 name that differs from Phoenix 1's only in
        // punctuation or spacing ("pt.2" vs "pt. 2") is drift, and drift is a dead title.
        var byShape = ChartTitles()
            .GroupBy(t => Shape(t.SongName), StringComparer.Ordinal)
            .Where(g => g.Select(t => (string)t.SongName).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => string.Join(" / ", g.Select(t => $"\"{t.SongName}\"")
                .Distinct(StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(byShape.Length == 0,
            "The same song is spelled two ways across the title lists — one of them cannot match a " +
            "chart: " + string.Join(" · ", byShape));
    }

    [Fact]
    public void EveryChartTitleNamesANonEmptySong()
    {
        var titles = ChartTitles();

        Assert.NotEmpty(titles);
        Assert.All(titles, t => Assert.False(string.IsNullOrWhiteSpace(t.SongName)));
    }

    private static IReadOnlyList<ISpecificChartTitle> ChartTitles()
    {
        return PhoenixTitleList.BuildList().Concat(Phoenix2TitleList.BuildList())
            .OfType<ISpecificChartTitle>()
            .ToArray();
    }

    // Everything a song name can drift by without changing which song a human means: case, spaces,
    // and punctuation. Two names with the same shape are the same title claim.
    private static string Shape(Name songName)
    {
        var builder = new StringBuilder();
        foreach (var c in (string)songName)
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));

        return builder.ToString();
    }
}
