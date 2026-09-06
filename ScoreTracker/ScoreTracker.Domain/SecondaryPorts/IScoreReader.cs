using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     The Score Ledger's published read contract (ADR-001 D3 "pull"): every consumer
///     outside the Ledger reads scores through this, never through
///     <see cref="IPhoenixRecordRepository" /> (which becomes Ledger-internal at P5).
///     Methods are added per consumer migration — additive only.
/// </summary>
public interface IScoreReader
{
    /// <summary>A player's best attempt per chart in a mix.</summary>
    Task<IEnumerable<RecordedPhoenixScore>> GetBestScores(MixEnum mix, Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Bulk read for analytics: every player's best attempt in a mix's level×type folder.</summary>
    Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> GetScores(MixEnum mix, ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken);

    /// <summary>
    ///     Every player's best attempt on ONE chart — an indexed per-chart read, so a caller
    ///     that only wants a single chart's population doesn't scan the whole folder for it.
    /// </summary>
    Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> GetChartScores(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken);

    /// <summary>Best attempts for a set of players within a level range in a mix.</summary>
    Task<IEnumerable<RecordedPhoenixScore>> GetScores(MixEnum mix, IEnumerable<Guid> userIds, ChartType chartType,
        DifficultyLevel minimumLevel, DifficultyLevel maximumLevel, CancellationToken cancellationToken);

    /// <summary>Players holding a Perfect Game in a mix's level×type folder.</summary>
    Task<IEnumerable<(Guid UserId, Guid ChartId)>> GetPgUsers(MixEnum mix, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken);

    /// <summary>Best attempts for a set of players in a mix's level×type folder.</summary>
    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetPlayerScores(MixEnum mix,
        IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel difficulty, CancellationToken cancellationToken = default);

    /// <summary>Named best attempts for a set of players across a set of charts in a mix.</summary>
    Task<IEnumerable<UserPhoenixScore>> GetPlayerScores(MixEnum mix, IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Which of these players hold only a BROKEN best on which of these charts — the attempts the
    ///     pass-only reads above leave out on purpose. The peer standing popover counts them among
    ///     the peers who have not passed a chart, never as scores to rank against
    ///     (docs/design/peers-abstraction.md D9, D13).
    /// </summary>
    Task<IEnumerable<(Guid UserId, Guid ChartId)>> GetBrokenBests(MixEnum mix, IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Named best attempts for a set of players across a mix's level range and chart type.
    ///     <para>
    ///         The chart-id overload above takes the same shape as a list of GUIDs, which for a
    ///         cohort read is several hundred of them — a parameter list SQL Server plans badly.
    ///         A caller whose chart set IS a level band should ask for the band and narrow in
    ///         memory: one indexed range scan instead of a second giant IN.
    ///     </para>
    /// </summary>
    Task<IEnumerable<UserPhoenixScore>> GetPlayerScoresInLevelRange(MixEnum mix, IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel minimumLevel, DifficultyLevel maximumLevel,
        CancellationToken cancellationToken = default);

    /// <summary>Named best attempts for a set of players on one chart in a mix.</summary>
    Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(MixEnum mix, IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken = default);

    /// <summary>How many charts a player has cleared in a mix's level×type folder.</summary>
    Task<int> GetClearCount(MixEnum mix, Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     A player's journaled submission history on one chart in a mix, oldest first. Entries are
    ///     submissions as received (including ones that didn't beat the stored best), so
    ///     scores are not monotonic. History begins at the journal backfill (2026-06).
    /// </summary>
    Task<IEnumerable<ScoreJournalEntry>> GetScoreHistory(MixEnum mix, Guid userId, Guid chartId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     A player's journaled plays in a mix since a cutoff, oldest first, across every chart —
    ///     <see cref="GetScoreHistory" /> widened from one chart to a night.
    ///     <para>
    ///         Entries are submissions as received, so scores are not monotonic and stage breaks are
    ///         present: deciding which of them were a session is the caller's problem, and it needs
    ///         the ones that did not become records as much as the ones that did.
    ///     </para>
    ///     <para>
    ///         This exists as a port rather than a MediatR send because EventCompetition cannot
    ///         reference ScoreLedger: the edge would close a cycle through
    ///         Communities and Randomizer. Published ports are the sanctioned way out of exactly
    ///         that (ARCHITECTURE.md, "Verticals split by bounded context").
    ///     </para>
    /// </summary>
    /// <param name="until">
    ///     The far end of the range, or null for everything since the cutoff. The cap is applied from
    ///     the newest end, so a caller asking about one night in the past must bound both ends or the
    ///     cap slides off the night entirely and returns only plays that came after it.
    /// </param>
    Task<IReadOnlyList<ScoreJournalEntry>> GetRecentPlays(MixEnum mix, Guid userId, DateTimeOffset since,
        DateTimeOffset? until, int limit, CancellationToken cancellationToken = default);

    /// <summary>Users with any recorded best-attempt activity in a mix on or after the cutoff.</summary>
    Task<IReadOnlySet<Guid>> GetActiveUserIds(MixEnum mix, DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verified, passing current bests for a set of players in a mix — the read behind the
    ///     supplemented leaderboards. Verified means the record came from an official import,
    ///     or predates source capture (2026-07) and so is import-derived in all but the
    ///     unprovable case: a manual or CSV score is a number a human typed and may lower a
    ///     best, which is not something a public leaderboard should carry.
    ///     <para>
    ///         Broken and score-less records are excluded — a board row is a passing score.
    ///         Callers chunk their player set; the whole mix at once is several hundred
    ///         thousand rows.
    ///     </para>
    /// </summary>
    Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> GetVerifiedBests(MixEnum mix,
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Everyone holding a verified, passing record in a mix, with the date of their most
    ///     recent one — the roster the supplemented leaderboards are drawn from, and the
    ///     recency that settles a game tag two accounts both claim. One grouped read; the
    ///     caller decides who is public.
    /// </summary>
    Task<IReadOnlyList<(Guid UserId, DateTimeOffset LastRecordedAt)>> GetVerifiedRecordActivity(MixEnum mix,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Distinct calendar days with any journaled score event for the player in a mix.
    ///     The 2026-06 backfill dated rows at each record's last update, so this spans the
    ///     whole mix era as a lower bound on real play days.
    /// </summary>
    Task<int> GetPlayDayCount(MixEnum mix, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Distinct charts with any journaled submission per player in a mix, over the whole
    ///     journal span. Same backfill caveat as <see cref="GetPlayDayCount" /> — a lower bound,
    ///     not full history. Players with no journal rows are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetJournaledChartCounts(MixEnum mix, IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>Per-chart population counts for a mix: players with a scored record, and how many passed.</summary>
    Task<IEnumerable<ChartScoreAggregate>> GetChartScoreAggregates(MixEnum mix,
        CancellationToken cancellationToken = default);

    /// <summary>A player's best XX (legacy mix) attempt per chart. XX records are Ledger-owned too.</summary>
    Task<IEnumerable<BestXXChartAttempt>> GetBestXXAttempts(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>A player's best legacy-scoring attempt per chart in a specific XX-or-older mix.</summary>
    Task<IEnumerable<BestXXChartAttempt>> GetBestXXAttempts(MixEnum mix, Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     What each player's legacy record in a mix adds up to: the net score old arcade
    ///     boards ranked on, plus the SSS/SS/S/A tallies, which is what most legacy records
    ///     actually carry. One pass over the mix's rows — the sum and the tallies come off the
    ///     same scan rather than four round trips. Players with no records are absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, LegacyScoreTotals>> GetLegacyTotals(MixEnum mix,
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Named best attempts for a set of players across a set of charts on XX or older —
    ///     the legacy twin of <see cref="GetPlayerScores(MixEnum, IEnumerable{Guid}, IEnumerable{Guid}, CancellationToken)" />.
    ///     Separate because an era score does not fit a PhoenixScore and the letter has no
    ///     Phoenix column, not because the query differs.
    /// </summary>
    Task<IEnumerable<UserLegacyScore>> GetPlayerLegacyScores(MixEnum mix, IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default);
}
