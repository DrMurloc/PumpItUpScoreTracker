using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Dtos.ApiV2;

/// <summary>
///     A PIU Scores player.
///     <para>
///         Unlike v1's <c>PlayerDto</c>, a private player's real name is returned rather than
///         "Anonymous". v2 only reaches a player through a share, and a player who deliberately
///         connected to a tool has consented — handing back "Anonymous" would make the tool useless
///         to the person who opted in. <see cref="IsPublic" /> rides along so a tool that republishes
///         data can still respect it.
///     </para>
/// </summary>
public sealed class PlayerV2Dto
{
    public PlayerV2Dto(User user, string? gameTag)
    {
        UserId = user.Id;
        Username = user.Name.ToString();
        Country = user.Country?.ToString();
        AvatarUrl = user.ProfileImage.ToString();
        IsPublic = user.IsPublic;
        GameTag = gameTag;
    }

    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string? Country { get; set; }
    public string AvatarUrl { get; set; }
    public bool IsPublic { get; set; }

    /// <summary>
    ///     The in-game tag, most recently observed. One value, not one per mix: the tag is an AM Pass
    ///     account setting shared across Phoenix and Phoenix 2, and the per-mix rows we store are
    ///     snapshots taken by different scrapes rather than distinct identities.
    /// </summary>
    public string? GameTag { get; set; }
}

public sealed class JudgmentsDto
{
    public int Perfects { get; set; }
    public int Greats { get; set; }
    public int Goods { get; set; }
    public int Bads { get; set; }
    public int Misses { get; set; }

    /// <summary>
    ///     The longest unbroken run of the play, solved from the score and the five counts (the
    ///     official site prints no combo). Null when it cannot be solved — the chart's note count is
    ///     unknown, the counts do not cover the chart, or the play was a stage break.
    /// </summary>
    public int? MaxCombo { get; set; }
}

public sealed class PlayerScoreDto
{
    public PlayerScoreDto(RecordedPhoenixScore record, MixEnum mix, double? pumbility)
    {
        ChartId = record.ChartId;
        RecordedAt = record.RecordedDate;
        Source = record.Source;
        Score = record.Score;
        LetterGrade = record.Score?.LetterGradeFor(mix).GetName();
        Plate = record.Plate?.GetName();
        IsBroken = record.IsBroken;
        Pumbility = pumbility;
        Judgments = MapJudgments(record.Judgements);
    }

    public Guid ChartId { get; set; }

    /// <summary>
    ///     When PIU Scores wrote the record — not when the play happened, which we do not know.
    ///     There is exactly one date on a score, and this is it.
    /// </summary>
    public DateTimeOffset RecordedAt { get; set; }

    public string? Source { get; set; }

    /// <summary>
    ///     On a legacy mix this is an era-scale number that does not compare to a Phoenix score.
    ///     Check the envelope's scoringModel first.
    /// </summary>
    public int? Score { get; set; }

    public string? LetterGrade { get; set; }

    /// <summary>Null when <see cref="IsBroken" /> — the game awards no plate for a failed stage.</summary>
    public string? Plate { get; set; }

    public bool IsBroken { get; set; }
    public double? Pumbility { get; set; }

    /// <summary>
    ///     Null rather than zeroed when the source never carried them — a CSV or hand-entered score
    ///     has no judgment breakdown, and zeros would read as a perfect game.
    /// </summary>
    public JudgmentsDto? Judgments { get; set; }

    internal static JudgmentsDto? MapJudgments(JudgementCounts? counts)
    {
        return counts is null
            ? null
            : new JudgmentsDto
            {
                Perfects = counts.Perfects, Greats = counts.Greats, Goods = counts.Goods,
                Bads = counts.Bads, Misses = counts.Misses, MaxCombo = counts.MaxCombo
            };
    }
}

/// <summary>
///     One player's best on a chart, on a page that spans players: the per-player score row with
///     the player's identity in front of it. The fields after <see cref="GameTag" /> are the
///     per-player row's, byte for byte, so a tool parses one score shape.
/// </summary>
public sealed class ChartScoreDto
{
    public ChartScoreDto(Guid userId, string username, string? gameTag, PlayerScoreDto score)
    {
        UserId = userId;
        Username = username;
        GameTag = gameTag;
        ChartId = score.ChartId;
        RecordedAt = score.RecordedAt;
        Source = score.Source;
        Score = score.Score;
        LetterGrade = score.LetterGrade;
        Plate = score.Plate;
        IsBroken = score.IsBroken;
        Pumbility = score.Pumbility;
        Judgments = score.Judgments;
    }

    /// <summary>The PIU Scores account. The same id <c>/api/v2/players</c> lists.</summary>
    public Guid UserId { get; set; }

    /// <summary>The player's PIU Scores username.</summary>
    public string Username { get; set; }

    /// <summary>The in-game tag, most recently observed; null when no import has linked one.</summary>
    public string? GameTag { get; set; }

    public Guid ChartId { get; set; }

    /// <summary>When PIU Scores wrote the record — not when the play happened, which is not known.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>Where the record came from: <c>officialImport</c>, <c>csv</c>, <c>manual</c>, or null for a record older than source capture.</summary>
    public string? Source { get; set; }

    /// <summary>On a legacy mix this is an era-scale number that does not compare to a Phoenix score. Check the envelope's <c>scoringModel</c>.</summary>
    public int? Score { get; set; }

    public string? LetterGrade { get; set; }

    /// <summary>Null when <see cref="IsBroken" /> — the game awards no plate for a failed stage.</summary>
    public string? Plate { get; set; }

    public bool IsBroken { get; set; }

    /// <summary>What this score is worth in PUMBILITY under the mix's formula; null on a legacy mix.</summary>
    public double? Pumbility { get; set; }

    /// <summary>Null when the source never carried judgments — a CSV or hand-entered score.</summary>
    public JudgmentsDto? Judgments { get; set; }
}

/// <summary>
///     Every readable player's best on one chart. Passes first, highest score first; failed bests
///     follow. The scoring model rides the envelope because a page is always one mix, and
///     <c>total</c> is present because the rows are already in memory.
/// </summary>
public sealed class ChartScorePageDto
{
    public string Mix { get; set; } = string.Empty;

    /// <summary><c>phoenix</c> or <c>legacy</c>. Branch on this before reading <c>score</c>.</summary>
    public string ScoringModel { get; set; } = string.Empty;

    public ChartScoreDto[] Data { get; set; } = Array.Empty<ChartScoreDto>();

    /// <summary>How many rows were asked for — not how many arrived, which is <c>data.length</c>.</summary>
    public int Limit { get; set; }

    /// <summary>How many readable players hold a record on this chart in this mix.</summary>
    public int Total { get; set; }

    /// <summary>Absolute URL of the next page, or null on the last one. Follow it rather than constructing it.</summary>
    public string? Next { get; set; }
}

/// <summary>Scores in one mix. The scoring model rides the envelope because a page is always one mix.</summary>
public sealed class PlayerScorePageDto
{
    public string Mix { get; set; } = string.Empty;

    /// <summary><c>phoenix</c> or <c>legacy</c>. Branch on this before reading <c>score</c>.</summary>
    public string ScoringModel { get; set; } = string.Empty;

    public PlayerScoreDto[] Data { get; set; } = Array.Empty<PlayerScoreDto>();
    public int Limit { get; set; }
    public string? Next { get; set; }
}

public sealed class JournalEntryDto
{
    public JournalEntryDto(ScoreJournalEntry entry, MixEnum mix)
    {
        OccurredAt = entry.OccurredAt;
        Source = entry.Source;
        SessionId = entry.SessionId;
        ChartId = entry.ChartId;
        IsBest = entry.IsBest;
        Score = entry.Score;
        LetterGrade = entry.Score?.LetterGradeFor(mix).GetName();
        Plate = entry.Plate?.GetName();
        IsBroken = entry.IsBroken;
        IsStageBroken = entry.IsStageBroken;
        Judgments = PlayerScoreDto.MapJudgments(entry.Judgements);
    }

    public DateTimeOffset OccurredAt { get; set; }
    public string Source { get; set; }
    public Guid? SessionId { get; set; }
    public Guid ChartId { get; set; }

    /// <summary>False for a play the official site reported that never beat the player's best.</summary>
    public bool IsBest { get; set; }

    public int? Score { get; set; }
    public string? LetterGrade { get; set; }
    public string? Plate { get; set; }
    public bool IsBroken { get; set; }

    /// <summary>
    ///     The stage broke — the song ended before its last note. Always broken and never best, with
    ///     no score (the site prints none for one) and, when the recently-played card carried them,
    ///     the judgments up to where it stopped.
    /// </summary>
    public bool IsStageBroken { get; set; }

    public JudgmentsDto? Judgments { get; set; }
}

public sealed class SessionDto
{
    public SessionDto(RecentSessionsPage.SessionGroup group)
    {
        SessionId = group.SessionId;
        Mix = group.Mix.ToString();
        Source = group.Source;
        StartedAt = group.Start;
        LastActivityAt = group.End;
        ScoreCount = group.Rows.Count;
    }

    /// <summary>Null for activity that predates session capture, grouped by calendar day instead.</summary>
    public Guid? SessionId { get; set; }

    public string Mix { get; set; }
    public string Source { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public int ScoreCount { get; set; }
}

public sealed class WeeklyChartDto
{
    public Guid ChartId { get; set; }
}

public sealed class WeeklyChartScoreDto
{
    public Guid ChartId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? Plate { get; set; }
    public bool IsBroken { get; set; }
}
