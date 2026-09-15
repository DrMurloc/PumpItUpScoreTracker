namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The PIU Scores Hardmode board as ONE viewer may see it (docs/design/hardmode-leaderboard.md
///     D17): the rows they are allowed. Viewer-shaped rather than global because a private account
///     is on its own board and nobody else's — so <see cref="HardmodeBoardRow.Place" /> is a place
///     among what this viewer can see, and two viewers reading the same pool are handed different
///     lists by design.
///     <para>
///         The D17 filtering stands; only the line under the board saying how many were left out
///         is gone, and its count with it (D25). The standing above the rows is still counted over
///         the same population the rows come from, so the two cannot disagree.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeBoardRecord(IReadOnlyList<HardmodeBoardRow> Rows)
{
    public static HardmodeBoardRecord Empty { get; } = new(Array.Empty<HardmodeBoardRow>());
}

/// <summary>
///     One row of the PIU Scores Hardmode leaderboard. <paramref name="Hardmode" /> IS the
///     PUMBILITY this page ranks on — the player's ordinary pool is not carried here, because a
///     board that prints both invites the reading that they are two comparable numbers when one
///     is a strict subset of the other (owner, 2026-09-12).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HardmodeBoardRow(int Place, Guid UserId, double Hardmode, int Held);
