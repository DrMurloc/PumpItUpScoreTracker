using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     What counts as a personal best on XX and older, where the two axes move INDEPENDENTLY:
///     a run that beats your score but not your grade raises the score and leaves the grade
///     alone, and the reverse raises the grade and leaves the score alone. The stored best is
///     therefore a composite that no single play necessarily produced.
///
///     <para>
///         That is the exact opposite of <see cref="BestAttemptPolicy" />, and deliberately so
///         (owner, 2026-08-10). Do not unify them. Phoenix moves its axes together because
///         letting a plate win on its own dragged scores down — the plate leak, which has a
///         regression test named after it. Legacy has no such coupling to protect: an era score
///         is a raw point total and the letter grade is an accuracy verdict, so they are
///         genuinely separate measurements of the same run rather than two views of one number.
///         Making legacy behave like Phoenix would throw away a real improvement; making
///         Phoenix behave like legacy would reopen the leak.
///     </para>
///
///     <para>
///         Broken-ness travels with the grade, not the score: failing is a property of how the
///         run was judged, which is the same axis the letter sits on.
///     </para>
/// </summary>
public static class LegacyBestAttemptPolicy
{
    /// <summary>
    ///     Whether the grade axis improves. A pass outranks a break whatever the letters —
    ///     the same call Phoenix makes, for the same reason: clearing is the thing being
    ///     measured, and a good-looking fail is still a fail.
    /// </summary>
    public static bool GradeBeats(XXLetterGrade storedGrade, bool storedIsBroken,
        XXLetterGrade incomingGrade, bool incomingIsBroken)
    {
        if (storedIsBroken != incomingIsBroken) return storedIsBroken;

        return incomingGrade > storedGrade;
    }

    /// <summary>
    ///     Whether the score axis improves. A submission carrying no number never displaces a
    ///     stored one — most legacy records are grade-only, so "no score given" has to mean
    ///     "leave the score alone" rather than "the score is now nothing".
    /// </summary>
    public static bool ScoreBeats(XXScore? storedScore, XXScore? incomingScore)
    {
        if (incomingScore == null) return false;
        if (storedScore == null) return true;

        return (int)incomingScore.Value > (int)storedScore.Value;
    }

    /// <summary>Whether either axis improves — a submission that moves neither is not history.</summary>
    public static bool Beats(XXChartAttempt? stored, XXChartAttempt incoming)
    {
        if (stored == null) return true;

        return GradeBeats(stored.LetterGrade, stored.IsBroken, incoming.LetterGrade, incoming.IsBroken)
               || ScoreBeats(stored.Score, incoming.Score);
    }

    /// <summary>
    ///     The best after this submission: each axis independently keeps whichever side is
    ///     better. The date moves only when something actually improved, so a re-import that
    ///     changes nothing does not restamp a record as fresh.
    /// </summary>
    public static XXChartAttempt Merge(XXChartAttempt? stored, XXChartAttempt incoming,
        DateTimeOffset recordedOn)
    {
        if (stored == null) return incoming;

        var gradeImproved = GradeBeats(stored.LetterGrade, stored.IsBroken,
            incoming.LetterGrade, incoming.IsBroken);
        var scoreImproved = ScoreBeats(stored.Score, incoming.Score);
        if (!gradeImproved && !scoreImproved) return stored;

        return new XXChartAttempt(
            gradeImproved ? incoming.LetterGrade : stored.LetterGrade,
            gradeImproved ? incoming.IsBroken : stored.IsBroken,
            scoreImproved ? incoming.Score : stored.Score,
            recordedOn);
    }
}
