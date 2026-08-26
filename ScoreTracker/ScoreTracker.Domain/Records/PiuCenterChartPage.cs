namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     The skill-relevant slice of one piucenter per-chart page: their top-skill
    ///     summary plus per-segment badge tallies (SegmentSkillCounts[skill] = number of
    ///     chart segments carrying that badge, out of SegmentCount). The stepchart
    ///     rendering data in the same file is deliberately not surfaced — we link out
    ///     for that (design doc §8a ingestion boundary).
    ///     <para>
    ///         The note-shape counts read the same file's tap and hold arrays: TapRows is
    ///         distinct tap start times (a jump is one row and one judgement; a rolling
    ///         bracket is one row per arrow), HoldRows the same over holds, and
    ///         HoldTickSum their "Hold ticks" tally. The tick sum carries their
    ///         pre-Phoenix hold model — diagnostics, never display
    ///         (docs/design/phoenix-score-calculator.md D13).
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterChartPage(
        string ExternalKey,
        IReadOnlyList<string> SkillSummary,
        int SegmentCount,
        IReadOnlyDictionary<string, int> SegmentSkillCounts,
        IReadOnlyDictionary<string, int> RareSkillCounts,
        IReadOnlyList<string> LastSegmentSkills,
        bool LastSegmentIsPeak,
        decimal? Nps,
        string? NotetypeBpmSummary,
        string? SordChartLevel,
        int TapRows = 0,
        int HoldRows = 0,
        int HoldTickSum = 0,
        PiuCenterCrux? Crux = null,
        StanceProfile? Stance = null);

    /// <summary>
    ///     The chart at its hardest: the FIRST segment reaching the page's maximum modelled
    ///     level, described relative to the chart around it
    ///     (docs/design/chart-identity.md §4).
    ///     <para>
    ///         <paramref name="Peakiness" /> is the crux level against the level the game prints
    ///         — positive is a spike, negative is a chart whose difficulty is duration rather
    ///         than any one passage. It is null when the page carries no readable METER, which
    ///         is the only piece here that depends on one.
    ///     </para>
    ///     <para>
    ///         Their per-segment level is a model measured against that printed level: good for
    ///         banding, not for arithmetic. <paramref name="Enps" /> is likewise theirs, and must
    ///         not be compared against the page's own peak-ish nps summary as a ratio.
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterCrux(
        decimal Level,
        decimal? Peakiness,
        decimal Position,
        decimal Duration,
        decimal? Enps,
        IReadOnlyList<string> Badges);
}
