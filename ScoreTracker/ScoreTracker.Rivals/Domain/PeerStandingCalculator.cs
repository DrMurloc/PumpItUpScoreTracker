using ScoreTracker.Domain.Models;

namespace ScoreTracker.Rivals.Domain;

/// <summary>
///     The arithmetic behind a <see cref="PeerStanding" /> (docs/design/peers-abstraction.md §4.1),
///     kept pure so the rules are testable without a database: passes only enter the ladder (D9),
///     a place is read inside a cohort that includes the subject (D10), and every ticked source
///     gets its own line even when nobody in it has passed the chart.
/// </summary>
internal static class PeerStandingCalculator
{
    public const int PerfectGame = 1_000_000;

    /// <summary>
    ///     One peer's passing score on the chart. The key is a <see cref="PeerVoice" />, so an
    ///     account, a board-only rival and a player the official board is the only record of all
    ///     sit in one set without any of them having to borrow another's kind of id.
    /// </summary>
    public sealed record PeerPass(PeerVoice PlayerKey, int Score, bool FromOfficialBoard);

    /// <summary>A ticked source and the player keys it contributes (the subject already removed).</summary>
    public sealed record SourceMembers(
        PeerSourceKind Kind,
        Guid? CommunityId,
        string? CommunityName,
        bool IsRegional,
        bool IsWorld,
        IReadOnlySet<PeerVoice> Members);

    /// <summary>
    ///     <paramref name="passes" /> and <paramref name="brokenKeys" /> may carry players outside
    ///     <paramref name="union" /> (a shared read serving several charts); only union members count.
    /// </summary>
    public static PeerStanding Compute(int subjectScore, IReadOnlyCollection<PeerPass> passes,
        IReadOnlySet<PeerVoice> brokenKeys, IReadOnlyList<SourceMembers> sources, IReadOnlySet<PeerVoice> union,
        DateTimeOffset? officialAsOf)
    {
        // One pass per player: a key that arrives twice (a site rival read once by the union and
        // once by the rival reader) keeps its higher score and is counted once.
        var best = new Dictionary<PeerVoice, PeerPass>();
        foreach (var pass in passes)
        {
            if (!union.Contains(pass.PlayerKey)) continue;
            if (!best.TryGetValue(pass.PlayerKey, out var existing) || pass.Score > existing.Score)
                best[pass.PlayerKey] = pass;
        }

        var passed = best.Count;
        var better = best.Values.Count(p => p.Score > subjectScore);
        var perfectGames = best.Values.Count(p => p.Score == PerfectGame);
        // A player with a pass and a stale broken row is a passer; only the ones with nothing
        // but a break count as broke.
        var broke = brokenKeys.Count(k => union.Contains(k) && !best.ContainsKey(k));
        var boardRows = best.Values.Any(p => p.FromOfficialBoard);

        var lines = sources.Select(source =>
        {
            var sourcePassed = 0;
            var sourceBetter = 0;
            var fromBoard = 0;
            foreach (var member in source.Members)
            {
                if (!best.TryGetValue(member, out var pass)) continue;
                sourcePassed++;
                if (pass.Score > subjectScore) sourceBetter++;
                if (pass.FromOfficialBoard) fromBoard++;
            }

            return new PeerStandingSource(source.Kind, source.CommunityId, source.CommunityName, source.IsRegional,
                source.IsWorld, source.Members.Count, sourcePassed, sourceBetter, fromBoard);
        }).ToArray();

        return passed == 0
            ? PeerStanding.NoCohort(union.Count, broke, lines)
            : new PeerStanding(union.Count, passed, better, perfectGames, broke, lines,
                boardRows ? officialAsOf : null);
    }
}
