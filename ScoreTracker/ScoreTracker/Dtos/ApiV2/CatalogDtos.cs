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

    public string DisplayName { get; set; }
    public int SortOrder { get; set; }
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

    public string Type { get; set; }
    public string Artist { get; set; }
    public int DurationSeconds { get; set; }
    public string ImageUrl { get; set; }
    public BpmDto? Bpm { get; set; }
}

public sealed class BpmDto
{
    public decimal? Min { get; set; }
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

    public Guid Id { get; set; }

    /// <summary>The mix this expression of the chart belongs to — level and note count are per-mix.</summary>
    public string Mix { get; set; }

    public string OriginalMix { get; set; }
    public string SongName { get; set; }
    public string ImageUrl { get; set; }
    public string Type { get; set; }
    public int Level { get; set; }

    /// <summary>
    ///     Human-facing difficulty. Slot-aware, so a pre-Exceed chart reads "Crazy 6" rather than an
    ///     ambiguous "S6" — the same song can carry Hard 6 and Crazy 6 in that era.
    /// </summary>
    public string Difficulty { get; set; }

    public int? NoteCount { get; set; }
    public int PlayerCount { get; set; }
    public string? StepArtist { get; set; }
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

public sealed class TierListEntryV2Dto
{
    public Guid ChartId { get; set; }
    public string Category { get; set; } = string.Empty;
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

    public Guid ChartId { get; set; }
    public int? DataVersion { get; set; }

    /// <summary>
    ///     PIU Center's own measurement. Do not derive this from note count over duration — that is a
    ///     different number, and recomputing it will disagree with every other consumer.
    /// </summary>
    public double? Nps { get; set; }

    public double? DifficultyPrediction { get; set; }
    public double? SustainTimeSeconds { get; set; }
    public double? TimeUnderTensionSeconds { get; set; }
    public bool? LastSegmentIsPeak { get; set; }
    public ChartSkillDto[] Skills { get; set; }
    public RarePatternDto[] RarePatterns { get; set; }
}

public sealed class ChartSkillDto
{
    /// <summary>piucenter's raw badge name, not the site's display skill vocabulary.</summary>
    public string Name { get; set; } = string.Empty;

    public double? Fraction { get; set; }
    public int? Top3Rank { get; set; }
    public int? PracticeRank { get; set; }
    public bool InLastSegment { get; set; }
}

public sealed class RarePatternDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class SimilarChartDto
{
    public Guid ChartId { get; set; }
    public double Score { get; set; }
    public double SkillScore { get; set; }
    public double IntensityScore { get; set; }
    public SharedBadgeDto[] SharedBadges { get; set; } = Array.Empty<SharedBadgeDto>();
}

public sealed class SharedBadgeDto
{
    /// <summary>piucenter's raw badge name. Coverage is the fraction BOTH charts carry.</summary>
    public string Badge { get; set; } = string.Empty;

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

    public SimilarChartDto[] Data { get; set; } = Array.Empty<SimilarChartDto>();
}
