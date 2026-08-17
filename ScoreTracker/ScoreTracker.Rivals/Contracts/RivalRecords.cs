using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Rivals.Contracts;

/// <summary>
///     Somebody who rivals you. <see cref="IsMutual" /> is what turns the reverse list from a
///     stranger-count into something readable — most of these will be people you already chose.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalOfMeRecord(
    Guid EdgeId,
    Guid UserId,
    string PlayerName,
    Uri Avatar,
    bool IsPublic,
    bool SharesCommunity,
    bool IsMutual,
    DateTimeOffset AddedAt);

[ExcludeFromCodeCoverage]
public sealed record BlockedPlayerRecord(Guid UserId, string PlayerName, Uri Avatar, DateTimeOffset BlockedAt);

/// <summary>
///     What the invite landing page shows before you commit. Deliberately just the person: a code
///     is a handshake, not a profile.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalInvitePreviewRecord(Guid UserId, string PlayerName, Uri Avatar, bool AlreadyRival);

/// <summary>
///     A pickable player. <see cref="AlreadyRival" /> so the picker can say so rather than
///     offering a button that resolves to the edge you already have.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalCandidateRecord(Guid UserId, string PlayerName, Uri Avatar, bool IsPublic,
    bool SharesCommunity, bool AlreadyRival);

/// <summary>
///     Rival scores keyed by chart, plus the instant the OFFICIAL half was true. A caller renders
///     <see cref="OfficialAsOf" /> once per board as a footnote rather than per row — a marker on
///     every line is a disclaimer, and the asterisk is enough (D27).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalChartScores(
    DateTimeOffset? OfficialAsOf,
    IReadOnlyDictionary<Guid, IReadOnlyList<RivalChartScore>> ByChart)
{
    public static readonly RivalChartScores Empty =
        new(null, new Dictionary<Guid, IReadOnlyList<RivalChartScore>>());
}

/// <summary>
///     One rival's score on one chart. <see cref="Source" /> is what a surface reads to decide
///     whether the row needs an asterisk.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalChartScore(
    Guid EdgeId,
    Guid? UserId,
    string? Tag,
    string DisplayName,
    Uri? Avatar,
    // An era score fits here already — this has always been an int rather than a PhoenixScore.
    // LegacyGrade is what Phoenix has no column for: on XX and older the letter IS the result,
    // and most legacy records carry no number at all.
    int Score,
    PhoenixPlate? Plate,
    bool IsBroken,
    RivalScoreSource Source,
    XXLetterGrade? LegacyGrade = null);

public enum RivalScoreSource
{
    /// <summary>A live best attempt from the ledger.</summary>
    Site,

    /// <summary>A placement from the last sealed weekly mirror — up to seven days old.</summary>
    Official
}

/// <summary>
///     A comparison. <see cref="Capabilities" /> travels with it so the renderer knows which
///     sections are ABSENT-with-a-reason rather than merely empty (D29). The tallies count the
///     charts you have BOTH scored; <see cref="OnlyYou" /> and <see cref="OnlyThem" /> count the
///     rows only one of you has, which ride along in <see cref="Rows" /> with the other side null so
///     a switch can show or hide them without another read. A board-only subject never has
///     one-sided rows — "boards you're both on" is its whole universe.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalHeadToHeadRecord(
    HeadToHeadSubject Subject,
    int YouAhead,
    int TheyAhead,
    int Shared,
    DateTimeOffset? OfficialAsOf,
    IReadOnlyList<RivalHeadToHeadRow> Rows,
    int OnlyYou = 0,
    int OnlyThem = 0)
{
    public RivalCapabilities Capabilities => Subject.Capabilities;
}

[ExcludeFromCodeCoverage]
public sealed record RivalHeadToHeadRow(
    Guid ChartId,
    int? YourScore,
    int? TheirScore,
    RivalScoreSource TheirSource,
    // Both sides carry the plate and the break so the row can render the same score vocabulary
    // every other board uses. A bare number next to a letter grade elsewhere reads as a
    // different, lesser table.
    PhoenixPlate? YourPlate = null,
    bool YourIsBroken = false,
    PhoenixPlate? TheirPlate = null,
    bool TheirIsBroken = false,
    // On a legacy mix the letter is the result and the plate columns stay null. The scores
    // above still rank the row: era scores ARE comparable within one chart, which is the only
    // comparison this table makes.
    XXLetterGrade? YourLegacyGrade = null,
    XXLetterGrade? TheirLegacyGrade = null);
