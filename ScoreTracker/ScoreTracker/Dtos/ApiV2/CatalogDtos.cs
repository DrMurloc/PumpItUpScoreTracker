using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Dtos.ApiV2;

/// <summary>
///     api/v2's catalog wire shapes. Deliberately separate types from v1's
///     <see cref="ScoreTracker.Web.Dtos.Api" /> namespace: those are pinned by approval goldens, so
///     sharing a class would make any v2 shape change a v1 breaking change.
/// </summary>
public sealed class MixDto
{
    public MixDto(MixRecord record)
    {
        Name = record.Name;
        DisplayName = record.DisplayName;
        SortOrder = record.SortOrder;
        IsPrimary = record.IsPrimary;
        ScoringModel = record.UsesLegacyScoring ? "legacy" : "phoenix";
    }

    /// <summary>The value every other endpoint's <c>mix</c> parameter takes.</summary>
    public string Name { get; set; }

    /// <summary>The mix's name as the game prints it, for display.</summary>
    public string DisplayName { get; set; }

    /// <summary>Release order, oldest first. Sort on this rather than on the name.</summary>
    public int SortOrder { get; set; }

    /// <summary>True for the mixes the site's mix picker shows directly; the rest live behind "More".</summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    ///     <c>phoenix</c> — a 1M-scale score with a plate. <c>legacy</c> — a letter grade with an
    ///     optional era-scale number that does not compare to a Phoenix score. Branch on this before
    ///     reading any score from this mix.
    /// </summary>
    public string ScoringModel { get; set; }
}

public sealed class SongV2Dto
{
    public SongV2Dto(SongRecord record)
    {
        Name = record.Name.ToString();
        Type = record.Type.ToString();
        Artist = record.Artist.ToString();
        DurationSeconds = (int)record.Duration.TotalSeconds;
        ImageUrl = record.ImagePath.ToString();
        Bpm = record.MinBpm is null && record.MaxBpm is null
            ? null
            : new BpmDto { Min = record.MinBpm, Max = record.MaxBpm };
    }

    /// <summary>Songs are keyed by name — the catalog has no song id.</summary>
    public string Name { get; set; }

    /// <summary>Arcade, ShortCut, FullSong or Remix — the song's cut, which decides its length and where it sits in the game's folders.</summary>
    public string Type { get; set; }

    /// <summary>The credited artist.</summary>
    public string Artist { get; set; }

    /// <summary>The song's length, in whole seconds.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>The song's jacket image.</summary>
    public string ImageUrl { get; set; }

    /// <summary>The BPM range the game shows; null when the catalog has none for this song.</summary>
    public BpmDto? Bpm { get; set; }
}

/// <summary>A BPM range. Min and max are equal on a song with one tempo.</summary>
public sealed class BpmDto
{
    /// <summary>The lowest BPM the song reaches.</summary>
    public decimal? Min { get; set; }

    /// <summary>The highest BPM the song reaches.</summary>
    public decimal? Max { get; set; }
}

public sealed class ChartV2Dto
{
    public ChartV2Dto(Chart chart, double? scoringLevel)
    {
        ScoringLevel = scoringLevel;
        Id = chart.Id;
        Mix = chart.Mix.ToString();
        OriginalMix = chart.OriginalMix.ToString();
        SongName = chart.Song.Name.ToString();
        ImageUrl = chart.Song.ImagePath.ToString();
        Type = chart.Type.ToString();
        Level = chart.Level;
        Difficulty = chart.DifficultyDisplay;
        NoteCount = chart.NoteCount;
        PlayerCount = chart.PlayerCount;
        StepArtist = chart.StepArtist?.ToString();
        LegacySlot = chart.Slot?.ToString();
    }

    /// <summary>The chart's id — the same GUID across mixes, and the id every score row carries.</summary>
    public Guid Id { get; set; }

    /// <summary>The mix this expression of the chart belongs to — level and note count are per-mix.</summary>
    public string Mix { get; set; }

    /// <summary>The mix the chart first appeared in.</summary>
    public string OriginalMix { get; set; }

    /// <summary>The song's name; songs are keyed by name in <c>/api/v2/songs</c>.</summary>
    public string SongName { get; set; }

    /// <summary>The song's jacket image.</summary>
    public string ImageUrl { get; set; }

    /// <summary>Single, Double, CoOp, SinglePerformance or DoublePerformance.</summary>
    public string Type { get; set; }

    /// <summary>The level the game prints on the folder in this mix.</summary>
    public int Level { get; set; }

    /// <summary>
    ///     Human-facing difficulty. Slot-aware, so a pre-Exceed chart reads "Crazy 6" rather than an
    ///     ambiguous "S6" — the same song can carry Hard 6 and Crazy 6 in that era.
    /// </summary>
    public string Difficulty { get; set; }

    /// <summary>How many judged notes the chart has in this mix; null where the catalog has not counted it.</summary>
    public int? NoteCount { get; set; }

    /// <summary>How many players the chart is for — 1, or 2 and up for a co-op chart.</summary>
    public int PlayerCount { get; set; }

    /// <summary>The credited step artist, when known.</summary>
    public string? StepArtist { get; set; }

    /// <summary>
    ///     On a pre-Exceed chart, the slot it lived in — Normal, Hard, Crazy, Freestyle, Nightmare
    ///     and their doubles kin — which is what told two charts of one level apart. Null on any
    ///     chart from a mix that names charts by level.
    /// </summary>
    public string? LegacySlot { get; set; }

    /// <summary>
    ///     How hard the chart is to <i>score</i> on, as a float — a different question from
    ///     <see cref="Level" />, which is what the game prints on the folder. Per mix, because a
    ///     chart's scoring difficulty moves when its steps do.
    ///     <para>
    ///         <b>Null where we have no measurement</b>, which today is every mix except Phoenix and
    ///         XX. Null is not zero and not "easy" — it means unknown.
    ///     </para>
    /// </summary>
    public double? ScoringLevel { get; set; }
}

/// <summary>One chart's place on a tier list.</summary>
public sealed class TierListEntryV2Dto
{
    /// <summary>The chart, as <c>/api/v2/charts</c> ids it.</summary>
    public Guid ChartId { get; set; }

    /// <summary>
    ///     The difficulty band relative to the chart's level: Overrated, VeryEasy, Easy, Medium,
    ///     Hard, VeryHard, Underrated — or Unrecorded when the list has nothing on the chart yet.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Position within the band; lower comes first.</summary>
    public int Order { get; set; }
}

public sealed class ChartSkillProfileDto
{
    public ChartSkillProfileDto(ChartSkillProfile profile)
    {
        ChartId = profile.ChartId;
        DataVersion = profile.DataVersion;
        Nps = profile.Nps;
        DifficultyPrediction = profile.DifficultyPrediction;
        SustainTimeSeconds = profile.SustainTimeSeconds;
        TimeUnderTensionSeconds = profile.TimeUnderTensionSeconds;
        LastSegmentIsPeak = profile.LastSegmentIsPeak;
        Skills = profile.Skills.Select(s => new ChartSkillDto
        {
            Name = s.Name,
            Fraction = s.Fraction,
            Top3Rank = s.Top3Rank,
            PracticeRank = s.PracticeRank,
            InLastSegment = s.InLastSegment
        }).ToArray();
        RarePatterns = profile.RarePatterns
            .Select(r => new RarePatternDto { Name = r.Name, Count = r.Count }).ToArray();
    }

    /// <summary>The chart, as <c>/api/v2/charts</c> ids it. The analysis is the same in every mix.</summary>
    public Guid ChartId { get; set; }

    /// <summary>Which release of PIU Center's analysis this row came from; null where it was not recorded.</summary>
    public int? DataVersion { get; set; }

    /// <summary>
    ///     PIU Center's own measurement. Do not derive this from note count over duration — that is a
    ///     different number, and recomputing it will disagree with every other consumer.
    /// </summary>
    public double? Nps { get; set; }

    /// <summary>PIU Center's model's own guess at the chart's level, as a float; null where it made none.</summary>
    public double? DifficultyPrediction { get; set; }

    /// <summary>How long the chart's longest sustained run lasts, in seconds.</summary>
    public double? SustainTimeSeconds { get; set; }

    /// <summary>How many seconds of the chart are spent under load rather than resting.</summary>
    public double? TimeUnderTensionSeconds { get; set; }

    /// <summary>True when the chart's hardest stretch is its last one — the kind that fails a tired player at the end.</summary>
    public bool? LastSegmentIsPeak { get; set; }

    /// <summary>The step patterns PIU Center found in the chart, with how much of it each covers.</summary>
    public ChartSkillDto[] Skills { get; set; }

    /// <summary>Patterns that are rare across the catalog and appear in this chart, with counts.</summary>
    public RarePatternDto[] RarePatterns { get; set; }
}

/// <summary>One step pattern in a chart, as PIU Center's analysis names it.</summary>
public sealed class ChartSkillDto
{
    /// <summary>piucenter's raw badge name, not the site's display skill vocabulary.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The share of the chart this pattern covers, 0 to 1.</summary>
    public double? Fraction { get; set; }

    /// <summary>This pattern's rank among the chart's three most prominent, 1 to 3; null when it is not one of them.</summary>
    public int? Top3Rank { get; set; }

    /// <summary>Where PIU Center ranks this chart as practice for the pattern; null when it does not recommend it.</summary>
    public int? PracticeRank { get; set; }

    /// <summary>True when the pattern shows up in the chart's final stretch.</summary>
    public bool InLastSegment { get; set; }
}

/// <summary>A step pattern that is rare across the catalog, and how often this chart uses it.</summary>
public sealed class RarePatternDto
{
    /// <summary>piucenter's raw pattern name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How many times the pattern occurs in the chart.</summary>
    public int Count { get; set; }
}

/// <summary>A chart that plays like the one asked about, and why.</summary>
public sealed class SimilarChartDto
{
    /// <summary>The chart, as <c>/api/v2/charts</c> ids it.</summary>
    public Guid ChartId { get; set; }

    /// <summary>Overall similarity, 0 to 1. Compare against the envelope's <c>matchFloor</c> to decide what counts as a match.</summary>
    public double Score { get; set; }

    /// <summary>The part of the score that comes from shared step patterns.</summary>
    public double SkillScore { get; set; }

    /// <summary>The part of the score that comes from matching intensity — density, sustain and tension.</summary>
    public double IntensityScore { get; set; }

    /// <summary>The patterns both charts carry, with how much of each chart they cover.</summary>
    public SharedBadgeDto[] SharedBadges { get; set; } = Array.Empty<SharedBadgeDto>();
}

/// <summary>A step pattern two charts have in common.</summary>
public sealed class SharedBadgeDto
{
    /// <summary>piucenter's raw badge name. Coverage is the fraction BOTH charts carry.</summary>
    public string Badge { get; set; } = string.Empty;

    /// <summary>The coverage the two charts share for this pattern, 0 to 1.</summary>
    public double Coverage { get; set; }
}

/// <summary>
///     Similar charts, with the two numbers a reader needs to interpret them.
/// </summary>
public sealed class SimilarChartsDto
{
    /// <summary>
    ///     How many charts the filters reached. Not a statistic — it is what turns "1 match" from a
    ///     bug report into "compared 30 charts within 2 levels, 1 match".
    /// </summary>
    public int ChartsCompared { get; set; }

    /// <summary>
    ///     The score at or above which a row is a real match. Published rather than applied: the list
    ///     runs down into the tail on purpose, because where the bar falls is the reader's decision,
    ///     and a reader that hardcodes 0.55 will not follow us if it moves.
    /// </summary>
    public double MatchFloor { get; set; }

    /// <summary>The matches, best first, running past <see cref="MatchFloor" /> into the tail on purpose.</summary>
    public SimilarChartDto[] Data { get; set; } = Array.Empty<SimilarChartDto>();
}
