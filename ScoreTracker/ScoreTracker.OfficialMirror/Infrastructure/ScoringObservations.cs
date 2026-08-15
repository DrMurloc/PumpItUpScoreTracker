using Microsoft.Extensions.Logging;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.OfficialMirror.Infrastructure;

/// <summary>
///     Instrumentation (2026-08-08): logs the score→grade and per-chart PUMBILITY observations
///     that fly past during an import. Every constant it was built to settle — the Superb Game
///     plate bonus, the grade ladders on both types end to end, the C/D/F score floors — is
///     settled as of 2026-08-14, and it stays in anyway, because it is the standing tripwire
///     for the one thing no deliberate play can reach (owner, 2026-08-14):
///     <para>
///         <b>Base(28) and Base(29)</b>, extrapolated to 290/300 and never priced — the five
///         charts up there are beyond deliberate reach, so the first import whose pool carries
///         one logs the row that solves the base outright, level and grade and plate and
///         official value all on the line. Tear this out only when that is closed or abandoned.
///     </para>
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
    ///     <para>
    ///         Which mix to run this for is the caller's call. It is Phoenix 2 only today, since
    ///         only Phoenix 2's C/D/F floors are still working values.
    ///     </para>
    /// </summary>
    public static void ObserveGrades(ILogger logger, MixEnum mix,
        IEnumerable<PiuGameGetRecentScoresResult> plays)
    {
        // Every line here is built out of formatting and arithmetic that is pure waste if the
        // level is switched off, and this runs inside an import — so ask once rather than
        // paying per play (CA1873).
        if (!logger.IsEnabled(LogLevel.Information)) return;

        foreach (var play in plays)
        {
            if (play.Grade == null) continue;

            var ours = play.Score.LetterGradeFor(mix);
            var disagrees = play.Grade.Value != ours;

            // A play that agrees with us and sits above the unverified band says nothing worth
            // a line. WHICH MIX this runs for is the caller's decision, not this method's —
            // detection here, policy there.
            if (!disagrees && (int)play.Score >= UnverifiedGradeCeiling) continue;

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
        // As in ObserveGrades: fifty rows of formatting per import is not worth paying for
        // when the level is off (CA1873).
        if (!logger.IsEnabled(LogLevel.Information)) return;

        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        foreach (var entry in entries)
        {
            // Zero is how the page prices a broken, co-op or sub-10 chart. Those say nothing
            // about a multiplier, and dividing by a base we never applied would invent one.
            // A passing F is NOT in that list — it prices nonzero (the 2026-08-14 reversal),
            // so F rows flow through like any other.
            if (entry.Value <= 0 || entry.Grade == null) continue;

            var plate = entry.Plate ?? PhoenixPlate.RoughGame;
            var grade = entry.Grade.Value;
            var ours = config.GetScore(entry.ChartType, entry.Level, grade.GetMinimumScoreFor(mix), plate);

            // Both tables answer per chart type, so every constant on this line reads the one
            // that actually priced the row — otherwise a type-split cell reports an implied
            // value solved against a multiplier it was never scored with.
            var shippedGrade = config.LetterGradeModifierFor(grade, entry.ChartType);
            var shippedPlate = config.PlateModifierFor(plate, entry.ChartType);

            // The unit the multipliers apply to, recovered from our own formula rather than
            // recomputed here, so the singles level shift cannot drift out of step with it.
            var divisor = shippedGrade + shippedPlate;
            var unit = divisor > 0 ? ours / divisor : 0;

            logger.LogInformation(
                "ScoringObservation {Observation}: {Plate} {ChartType} lvl {Level} grade {Grade} — " +
                "official {Official}, ours {Ours}, delta {Delta}, verdict {Verdict}. " +
                "implied grade multiplier {ImpliedGrade} (ship {ShippedGrade}), " +
                "implied plate bonus {ImpliedPlate} (ship {ShippedPlate})",
                "PumbilityRow", plate.GetShorthand(), entry.ChartType, entry.Level, grade.GetName(),
                entry.Value.ToString("F2"), ours.ToString("F2"), (ours - entry.Value).ToString("F2"),
                Math.Abs(ours - entry.Value) <= PumbilityTolerance ? "match" : "MISMATCH",
                unit <= 0 ? "?" : (entry.Value / unit - shippedPlate).ToString("F4"),
                shippedGrade.ToString("F2"),
                unit <= 0 ? "?" : (entry.Value / unit - shippedGrade).ToString("F4"),
                shippedPlate.ToString("F3"));
        }
    }
}
