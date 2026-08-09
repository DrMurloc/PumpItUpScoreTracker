using Microsoft.Extensions.Logging;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Infrastructure;

/// <summary>
///     Temporary instrumentation (2026-08-08): logs the score→grade and per-chart PUMBILITY
///     observations that fly past during an import, so the telemetry accumulates the evidence
///     that settles the constants still being guessed at — the Superb Game plate bonus, the A+
///     and B grade multipliers, and the C/D/F score floors. Expected to be torn out once the
///     table is closed; it is one file and two call lines for exactly that reason.
///     <para>
///         Two properties keep it safe on the import path. It does <b>no I/O</b> — every method
///         is pure over already-parsed data plus an ILogger — so the worst case is a logged
///         exception rather than a stalled or failed import. And it carries <b>no player
///         identity</b>: a line is a fact about the formula (level, type, grade, plate, value),
///         never about whose account it came from.
///     </para>
/// </summary>
internal static class ScoringObservations
{
    /// <summary>Officials print two decimals, so anything past a cent is a real disagreement.</summary>
    private const double PumbilityTolerance = 0.011;

    /// <summary>Below this the Phoenix 2 floors are working values, so every sample is worth having.</summary>
    private const int UnverifiedGradeCeiling = 800_000;

    /// <summary>
    ///     Every recent play whose printed grade either contradicts our cutoff table or lands in
    ///     the band where that table is unverified. Broken plays count, and are most of the
    ///     point: the game still prints a grade for a failed stage, and a failed stage is where
    ///     low scores live.
    /// </summary>
    public static void ObserveGrades(ILogger logger, MixEnum mix,
        IEnumerable<PiuGameGetRecentScoresResult> plays)
    {
        foreach (var play in plays)
        {
            if (play.Grade == null) continue;

            var ours = play.Score.LetterGradeFor(mix);
            var disagrees = play.Grade.Value != ours;

            // Phoenix 1's cutoffs have been settled since launch and Phoenix 1 carries most of
            // the site's imports, so only a DISAGREEMENT earns a line there. The sub-800k
            // corpus is Phoenix 2's alone, because only its C/D/F floors are still guesses —
            // logging every Phoenix 1 low score would be thousands of lines about a table
            // nobody is questioning, which is also precisely the volume adaptive sampling eats.
            if (!disagrees && (mix != MixEnum.Phoenix2 || (int)play.Score >= UnverifiedGradeCeiling)) continue;

            // Each placeholder appears exactly ONCE. Message templates are positional, not
            // named — repeating one silently demands another argument and throws FormatException
            // at log time, which the caller's guard would then swallow into a stream of zero
            // observations that looks exactly like "nothing interesting happened".
            logger.LogInformation(
                "ScoringObservation {Observation}: {Mix} {Score} printed {SiteGrade}, we say {OurGrade} " +
                "({Verdict}); the floor we ship for the printed grade is {OurFloor}. broken={IsBroken}",
                disagrees ? "GradeDisagreement" : "LowBandGrade", mix, (int)play.Score,
                play.Grade.Value.GetName(), ours.GetName(), disagrees ? "MISMATCH" : "agrees",
                (int)play.Grade.Value.GetMinimumScoreFor(mix), play.IsBroken);
        }
    }

    /// <summary>
    ///     Every per-chart PUMBILITY row the official page prices, with what we would have priced
    ///     it and the constants its own number implies.
    ///     <para>
    ///         EVERY row, not only the ones that disagree. Silence from a mismatch-only filter is
    ///         ambiguous — a cell that never logs might be correct, or might simply never have
    ///         been seen, and four of the sixteen plate × chart-type cells have never been
    ///         observed anywhere (SG on both types, EG doubles, PG singles). Closing those needs
    ///         positive observation, so the verdict rides the line as a property and the
    ///         filtering happens at query time.
    ///     </para>
    ///     <para>
    ///         One row cannot separate the grade multiplier from the plate bonus, so both implied
    ///         values are printed, each holding the other at what we ship. In practice only one
    ///         of the pair is ever unknown, which makes the line the answer rather than the
    ///         input to an offline solve.
    ///     </para>
    /// </summary>
    public static void ObservePumbility(ILogger logger, MixEnum mix,
        IEnumerable<PiuGameGetPumbilityResult.Entry> entries)
    {
        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        foreach (var entry in entries)
        {
            // Zero is how the page prices a broken, co-op or sub-10 chart. Those say nothing
            // about a multiplier, and dividing by a base we never applied would invent one.
            if (entry.Value <= 0 || entry.Grade == null) continue;

            var plate = entry.Plate ?? PhoenixPlate.RoughGame;
            var grade = entry.Grade.Value;
            var ours = config.GetScore(entry.ChartType, entry.Level, grade.GetMinimumScoreFor(mix), plate);

            // The unit the multipliers apply to, recovered from our own formula rather than
            // recomputed here, so the singles level shift cannot drift out of step with it.
            var divisor = config.LetterGradeModifiers[grade] + config.PlateModifiers[plate];
            var unit = divisor > 0 ? ours / divisor : 0;

            logger.LogInformation(
                "ScoringObservation {Observation}: {Plate} {ChartType} lvl {Level} grade {Grade} — " +
                "official {Official}, ours {Ours}, delta {Delta}, verdict {Verdict}. " +
                "implied grade multiplier {ImpliedGrade} (ship {ShippedGrade}), " +
                "implied plate bonus {ImpliedPlate} (ship {ShippedPlate})",
                "PumbilityRow", plate.GetShorthand(), entry.ChartType, (int)entry.Level, grade.GetName(),
                entry.Value.ToString("F2"), ours.ToString("F2"), (ours - entry.Value).ToString("F2"),
                Math.Abs(ours - entry.Value) <= PumbilityTolerance ? "match" : "MISMATCH",
                unit <= 0 ? "?" : (entry.Value / unit - config.PlateModifiers[plate]).ToString("F4"),
                config.LetterGradeModifiers[grade].ToString("F2"),
                unit <= 0 ? "?" : (entry.Value / unit - config.LetterGradeModifiers[grade]).ToString("F4"),
                config.PlateModifiers[plate].ToString("F3"));
        }
    }
}
