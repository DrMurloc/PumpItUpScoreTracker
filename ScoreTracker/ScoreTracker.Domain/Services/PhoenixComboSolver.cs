using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Recovers the max combo behind a Phoenix score. Combo is the only unknown in the
///     formula once the five judgement counts are known, so it inverts:
///     <code>
///     score = (.995(P + .6G + .2Go + .1B) + .005C) / T * 1e6
///     =>  C = 200 * (score * T / 1e6 - .995(P + .6G + .2Go + .1B))
///     </code>
///     <para>
///         <b>The judgement counts must cover the whole chart.</b> The inversion needs the
///         denominator the game scored against, and a breakdown that stops short of the note
///         count is not it — a stage break is the obvious case, but a stale catalog note count
///         is the same problem wearing different clothes. When the sum does not match, this
///         answers null rather than a number nobody can check.
///     </para>
///     <para>
///         The site reports no max combo on any surface we read, so this is how the value is
///         known at all. It lives beside the forward formula in
///         <see cref="Domain.Records.ScoreScreen" />'s assembly so a round-trip test can pin
///         the pair, and outside Records because that folder is excluded from coverage.
///     </para>
///     <para>
///         One point of score is <c>T/5000</c> combo, so the rounding step the game applies —
///         which we do not know exactly — moves the answer by less than half a combo for any
///         chart under ~2,500 notes and is corrected by rounding to nearest. Longer charts can
///         land one combo out.
///     </para>
/// </summary>
public static class PhoenixComboSolver
{
    public static int? MaxComboFor(JudgementCounts? judgements, PhoenixScore? score, int? chartNoteCount)
    {
        if (judgements == null || score == null || chartNoteCount == null) return null;

        var total = judgements.NoteCount;
        if (total <= 0 || total != chartNoteCount.Value) return null;

        var judged = .995 * (judgements.Perfects
                             + .6 * judgements.Greats
                             + .2 * judgements.Goods
                             + .1 * judgements.Bads);
        var combo = 200.0 * ((int)score.Value * (double)total / 1_000_000.0 - judged);
        var rounded = (int)Math.Round(combo, MidpointRounding.AwayFromZero);

        // A combo outside [0, notes] means one of the inputs is lying about the other. Say
        // nothing rather than print it.
        return rounded < 0 || rounded > total ? null : rounded;
    }

    /// <summary>
    ///     The same breakdown carrying its solved combo — the shape both write paths and the
    ///     backfill store. Whatever combo the counts arrived with is replaced, not kept: the value
    ///     is a function of the other inputs, and a stale one (a corrected note count, an
    ///     earlier bug) must not survive a re-solve. Null in, null out.
    /// </summary>
    public static JudgementCounts? WithMaxCombo(JudgementCounts? judgements, PhoenixScore? score,
        int? chartNoteCount)
    {
        return judgements == null
            ? null
            : judgements with { MaxCombo = MaxComboFor(judgements, score, chartNoteCount) };
    }
}
