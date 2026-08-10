using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records;

/// <summary>
///     What a player's legacy record in one mix adds up to. This is how the old arcade boards
///     ranked: one accumulated number over everything you had ever played, so a player who
///     cleared the whole catalogue outranked a stronger player who cleared less of it. Lower
///     levels carry fewer notes and so score less, which is roughly what kept it fair
///     (owner, 2026-08-10 — he was 13th in the world on XX this way).
/// </summary>
/// <param name="NetScore">
///     Sum of every recorded era score in the mix. A <see cref="long" />, not an int: the
///     largest single legacy score in production is 45,282,000, and a full-catalogue player
///     passes int.MaxValue long before they run out of charts.
/// </param>
/// <param name="Scored">
///     How many of the player's records carry a number at all. Most do not — 4.8% of
///     production legacy records have one — so a bare NetScore of zero is nearly always
///     "recorded grades, never typed a score" rather than "played badly". Surfacing the
///     count is what stops the board reading as an insult.
/// </param>
/// <param name="Recorded">Records in the mix, scored or not.</param>
[ExcludeFromCodeCoverage]
public sealed record LegacyScoreTotals(
    Guid UserId,
    long NetScore,
    int Scored,
    int Recorded,
    int TripleS,
    int DoubleS,
    int SingleS,
    int A)
{
    public static LegacyScoreTotals Empty(Guid userId)
    {
        return new LegacyScoreTotals(userId, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>The tally for one grade, so a caller can render the four columns off a loop.</summary>
    public int CountOf(XXLetterGrade grade)
    {
        return grade switch
        {
            XXLetterGrade.SSS => TripleS,
            XXLetterGrade.SS => DoubleS,
            XXLetterGrade.S => SingleS,
            XXLetterGrade.A => A,
            _ => 0
        };
    }
}
