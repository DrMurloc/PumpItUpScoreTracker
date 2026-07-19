using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     The weekly board, display-ready: one summary per live (or past-week) chart with its
///     ranked head and the caller's own standing. Rows carry the full <see cref="User" /> so
///     consumers render <c>UserLabel</c> without a second identity lookup —
///     the read that lets the weekly page drop its <c>IUserRepository</c> injection.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WeeklyBoardView(
    IReadOnlyList<WeeklyBoardChartSummary> Charts,
    bool IsLive,
    bool SuggestionsAvailable);

/// <summary>
///     One chart's slice of the weekly board. <c>TopPlaces</c> is the ranked head (three rows,
///     tie-laddered by <c>WeeklyChartSuggestionPolicy.ProcessIntoPlaces</c>); <c>MyRow</c> is the
///     caller's entry wherever it ranks, null when they haven't played or no caller was given.
///     <c>InRangeTopPlaces</c>/<c>InRangeEntryCount</c> are the same board with out-of-band
///     players removed and places renumbered (M20) — both states ship so the relevant-players
///     switch swaps without a round trip.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WeeklyBoardChartSummary(
    Guid ChartId,
    DateTimeOffset ExpirationDate,
    int EntryCount,
    IReadOnlyList<WeeklyBoardRow> TopPlaces,
    WeeklyBoardRow? MyRow,
    bool IsSuggested,
    IReadOnlyList<WeeklyBoardRow> InRangeTopPlaces,
    int InRangeEntryCount);

/// <summary>
///     A ranked row on one weekly chart's board. <c>Player</c> is null for a deleted account.
///     <c>Source</c> is the trust tier of the ranked score (M5) — null on finished weeks, whose
///     histories don't carry it; the photo tier derives from <c>Entry.PhotoUrl</c>.
///     <c>WasWithinRange</c> is the suggestion band's verdict on the entry's stored competitive
///     level; <c>InRangePlace</c> is the row's rank among in-band entries only (null when the
///     row itself is out of band) — the number the relevant-players switch shows.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WeeklyBoardRow(int Place, User? Player, WeeklyTournamentEntry Entry,
    ChallengeEntrySource? Source = null, bool WasWithinRange = true, int? InRangePlace = null);
