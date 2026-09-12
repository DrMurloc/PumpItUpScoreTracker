using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     The Hardmode pool totals on player stats. Its own port rather than a pair of methods on
///     IPlayerStatsRepository: that one is allowlisted to two writers by an architecture ratchet,
///     and widening the allowlist for a feature that writes four columns would spend the ratchet
///     rather than respect it.
/// </summary>
internal interface IHardmodeRatingRepository
{
    /// <summary>
    ///     Writes one account's three totals and the held count of each — three different
///     top-fifties, so "40 of 50" combined and "12 of 50" doubles are both true of one player. Only touches accounts the census
    ///     found something for — an account with no qualifying score keeps its zeroes.
    /// </summary>
    Task Save(MixEnum mix, IReadOnlyCollection<HardmodeRatingRow> rows, CancellationToken cancellationToken);

    /// <summary>Zeroes the mix before a rebuild, so a player who lost a score does not keep last week's total.</summary>
    Task Clear(MixEnum mix, CancellationToken cancellationToken);

    Task<IReadOnlyList<HardmodeBoardRow>> GetBoard(MixEnum mix, ChartType? pool,
        CancellationToken cancellationToken);

    Task<HardmodeRatingRow?> Get(MixEnum mix, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    ///     One account's place and field on one pool, counted in SQL. The page needs three of
    ///     these and used to get them by materialising and ordering every PlayerStats row on the
    ///     mix, three times, for one row each.
    ///     <para>
    ///         Null when the account holds nothing on that pool: a zero is not a standing, which
    ///         is the same rule the board's own read applies.
    ///     </para>
    /// </summary>
    Task<(int Place, int Field)?> GetStanding(MixEnum mix, ChartType? pool, Guid userId,
        CancellationToken cancellationToken);
}

/// <summary>One account's Hardmode totals, all three pools at once because they are written together.</summary>
internal sealed record HardmodeRatingRow(Guid UserId, double Combined, double Singles, double Doubles,
    int Held, int SinglesHeld, int DoublesHeld);
