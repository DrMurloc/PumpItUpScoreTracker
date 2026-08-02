using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Everything the hero renders for one session, assembled once so the components stay
///     dispatch-free. See docs/design/session-breakdown.md §2.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SessionBreakdown(
    RecentSessionsPage.SessionGroup Group,
    ScoreSessionRecord? Session,
    IReadOnlyDictionary<Guid, Chart> Charts,
    IReadOnlyList<SessionScore> Scores,
    SessionCeremony Ceremony,
    IReadOnlyList<PlayerMilestoneRecord> Milestones,
    IReadOnlyList<SessionTitleBarModel> TitleBars,
    IReadOnlyList<SessionPeerBoard> PeerBoards,
    IReadOnlyDictionary<Guid, User> Peers)
{
    /// <summary>
    ///     The import's game tag, when this session came from one. The wrong-card case is
    ///     exactly when a player stares at a session thinking "these aren't my scores", so
    ///     naming the account it pulled from is worth the row it takes.
    /// </summary>
    public string? AccountTag => Session?.AccountTag;

    public int PassCount => Group.Rows.Count(r => r.Classification == ScoreEventClassification.NewPass);
    public int UpscoreCount => Group.Rows.Count(r => r.Classification == ScoreEventClassification.Upscore);

    /// <summary>
    ///     Denormalized off the session row where one exists, counted off the journal where it
    ///     does not. Sessions predate the ScoreSession table by design, and the page keeps them.
    /// </summary>
    public int PlayCount => Session?.ScoreCount ?? Group.Rows.Count;
}

/// <summary>One journal row with whatever capture learned about it.</summary>
[ExcludeFromCodeCoverage]
public sealed record SessionScore(
    RecentSessionsPage.ScoreEventRecord Row,
    Chart? Chart,
    HighlightFlags Flags,
    HighlightDetail? Detail)
{
    public bool IsFlagged => Flags != HighlightFlags.None;

    /// <summary>
    ///     Null when no competitive cohort could measure this score — co-op, or more than five
    ///     levels below the player's competitive level. The row then renders in plain ink and
    ///     says nothing about it (D11).
    /// </summary>
    public double? PeerPercentile => Detail?.PeerPercentile;
}

/// <summary>The band above the fold: what moved, and how far.</summary>
[ExcludeFromCodeCoverage]
public sealed record SessionCeremony(
    double CurrentPumbility,
    double? PumbilityOld,
    double? PumbilityNew,
    double? SinglesCompetitiveOld,
    double? SinglesCompetitiveNew,
    double? DoublesCompetitiveOld,
    double? DoublesCompetitiveNew,
    double CurrentSinglesCompetitive,
    double CurrentDoublesCompetitive,
    int? OfficialRank,
    int? OfficialRankOld,
    DateTimeOffset? OfficialRankAsOf)
{
    public double? PumbilityGain => PumbilityNew - PumbilityOld;

    /// <summary>
    ///     What the band leads with. The pool is a standing value the player always has, so a
    ///     session that did not move it still shows the real number — only the delta and the
    ///     "from" line are session-scoped. Reading the milestone alone printed 0 for every
    ///     session that predates capture, which is most of them.
    /// </summary>
    public double HeadlinePumbility => PumbilityNew ?? CurrentPumbility;

    /// <summary>
    ///     True once there is a headline worth the space. A session that moved nothing still
    ///     renders the band — the current numbers are the anchor — but the page can tell the
    ///     two apart.
    /// </summary>
    public bool AnythingMoved => PumbilityNew != null || SinglesCompetitiveNew != null
                                                      || DoublesCompetitiveNew != null;
}

/// <summary>
///     One progress bar: the title being worked on in one scope, and how far the session
///     travelled along it. Scope is a Phoenix difficulty LEVEL ("21" — those titles span both
///     chart types) or a Phoenix 2 pool.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SessionTitleBarModel(
    string Scope,
    string Title,
    double OldPercent,
    double NewPercent,
    int Current,
    int Required);

/// <summary>
///     Your clubmates on one chart. It is a leaderboard — ordered by score, with real places
///     over the whole club — but only the few nearest your competitive level are shown, so the
///     places are deliberately non-contiguous. Closeness picks WHO appears; score orders them.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SessionPeerBoard(Chart Chart, IReadOnlyList<SessionPeer> Peers);

/// <summary>One clubmate with the place they actually hold on that chart among your clubs.</summary>
[ExcludeFromCodeCoverage]
public sealed record SessionPeer(int Place, CommunityPeerScore Score);

/// <summary>
///     One session as the grid shows it. <c>TopCharts</c> and the counts come from the journal,
///     so every session ever recorded has them; <c>Headline</c> only exists where capture ran,
///     which is why the card is built to read without it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SessionHistoryRow(
    Guid? SessionId,
    DateOnly? Day,
    MixEnum Mix,
    string Source,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Passes,
    int Upscores,
    int Plays,
    IReadOnlyList<Chart> TopCharts,
    int MoreCharts,
    string LevelSpan,
    IReadOnlyList<PlayerMilestoneRecord> Headline,
    string? AccountTag)
{
    public TimeSpan Duration => End - Start;
}
