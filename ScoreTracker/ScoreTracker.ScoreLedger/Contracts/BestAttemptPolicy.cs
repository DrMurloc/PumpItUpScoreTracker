using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     What counts as a personal best on the Phoenix generation (docs/design/score-truth-model.md).
///     The Ledger owns the rule and publishes it, because the acquisition side has to page exactly
///     as far as there is new work to save — and a second, hand-written copy of the rule out there
///     is what let plate improvements drag scores down.
///
///     <para>
///         XX and older use <see cref="LegacyBestAttemptPolicy" />, whose axes move independently.
///         The two are opposite by design and must stay separate: unifying them either reopens
///         the plate leak here or discards real improvements there.
///     </para>
/// </summary>
public static class BestAttemptPolicy
{
    /// <summary>
    ///     Whether an incoming attempt replaces the stored one. A pass always outranks a break,
    ///     whatever the numbers; below that it is score, with plate as a tiebreak at equal
    ///     score and nothing more. Plate can never win on its own, and never drags a score
    ///     down with it — the axes move together or not at all.
    /// </summary>
    public static bool Beats(PhoenixScore? storedScore, PhoenixPlate? storedPlate, bool storedIsBroken,
        PhoenixScore? incomingScore, PhoenixPlate? incomingPlate, bool incomingIsBroken)
    {
        if (storedIsBroken != incomingIsBroken) return storedIsBroken;

        var stored = storedScore == null ? -1 : (int)storedScore.Value;
        var incoming = incomingScore == null ? -1 : (int)incomingScore.Value;
        if (incoming != stored) return incoming > stored;

        return incomingPlate > storedPlate;
    }

    /// <summary>Whether an incoming attempt replaces an existing record — null = no record yet.</summary>
    public static bool Beats(RecordedPhoenixScore? stored, PhoenixScore? incomingScore, PhoenixPlate? incomingPlate,
        bool incomingIsBroken)
    {
        return stored == null || Beats(stored.Score, stored.Plate, stored.IsBroken, incomingScore, incomingPlate,
            incomingIsBroken);
    }

    /// <summary>
    ///     The plate an attempt is stored with. A failed stage is awarded no plate by the game,
    ///     so anything derived for one is fabrication (the recent-play parser used to compute
    ///     Perfect Game for a walk-off, whose judgement counts are all zero).
    /// </summary>
    public static PhoenixPlate? PlateFor(bool isBroken, PhoenixPlate? plate)
    {
        return isBroken ? null : plate;
    }

    /// <summary>
    ///     A stage break — the song interrupted, the play ended before its last note — is never
    ///     a personal best. No opt-in reaches it and no source may seat one: the game gives it no
    ///     grade and no chart score (the running number the Phoenix 2 best list prints for one is
    ///     normalised over the notes judged so far, and reads like a near-pass), so it is a play
    ///     for the journal and nothing more (docs/design/stage-breaks-and-max-combo.md D10).
    ///     Published here beside <see cref="Beats" /> for the same reason that is: the acquisition
    ///     side must page and filter by exactly the rule the ledger will apply.
    /// </summary>
    public static bool CanBeRecord(bool isStageBroken)
    {
        return !isStageBroken;
    }

    /// <summary>
    ///     A break with nothing hit: someone started a song and let it fail out. The official
    ///     site records those; we never store one, in any table, for any reason. Recognized by a
    ///     breakdown with no perfect, great, good or bad in it — the misses are the life bar
    ///     draining, so "nothing judged" is the wrong test (a walk-off's card reads 0/0/0/0/51) —
    ///     or, on the redesigned best list, which carries no breakdown, by a broken card scoring
    ///     zero.
    /// </summary>
    public static bool IsWalkOff(bool isBroken, PhoenixScore? score, JudgementCounts? judgements)
    {
        if (!isBroken) return false;
        if (judgements != null)
            return judgements.Perfects + judgements.Greats + judgements.Goods + judgements.Bads == 0;

        return score != null && (int)score.Value == 0;
    }
}
