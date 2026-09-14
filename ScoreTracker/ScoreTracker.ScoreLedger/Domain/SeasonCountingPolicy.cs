using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     The counting rule (docs/design/seasons.md D15, §4.1) as a pure function: given one observed
///     score and the seasons that exist, which season — if any — should hold it as a seasonal best.
///     <para>
///         Three facts about the official site shape it. A <b>recently-played</b> play is stamped with
///         its own time, so a play dated inside a season's window belongs to that season, and a play
///         dated before it is an old score the site is only now showing us. A <b>best-list card</b> is
///         stamped with the chart's <i>first</i> play, so an in-season upscore can arrive wearing a
///         pre-season date — which is why a card that raised a record we already held counts for the
///         season running when we saw it, whatever its date says. And a <b>brand-new importer's</b>
///         old cards raise nothing, because there was nothing to raise, so they stay out.
///     </para>
///     <para>
///         A sealed season is never written (D13). During the seven-day grace an ended season is not
///         yet sealed, so an in-window play still lands on it. A card dated inside a season that has
///         already been sealed falls through to the raise clause rather than being dropped: the raise
///         is something the import watched happen, and the only season that can hold it is the
///         running one.
///     </para>
/// </summary>
internal static class SeasonCountingPolicy
{
    /// <summary>
    ///     The season to write, or null for "this is not a seasonal best".
    /// </summary>
    /// <param name="source">
    ///     The journal source. Only <c>officialImport</c> counts — the manual, CSV and API paths are
    ///     unverified and never seed a season's pool.
    /// </param>
    /// <param name="observedAt">The play's own time, or the card's date when that is all there is.</param>
    /// <param name="raisedExistingRecord">
    ///     True only for a best-list card that raised an all-time record the player already held. An
    ///     observed play passes false: it is judged on its date alone.
    /// </param>
    /// <param name="seasons">Every season the roll has opened.</param>
    /// <param name="now">The clock, for finding the running season the raise clause writes to.</param>
    public static SeasonId? SeasonFor(string source, DateTimeOffset observedAt, bool raisedExistingRecord,
        IReadOnlyList<SeasonRecord> seasons, DateTimeOffset now)
    {
        if (source != ScoreJournalEntry.OfficialImportSource) return null;

        var dated = seasons.FirstOrDefault(s => s.Holds(observedAt));
        if (dated is { IsSealed: false }) return dated.Id;

        if (!raisedExistingRecord) return null;

        var running = seasons.FirstOrDefault(s => s.Holds(now));
        return running is { IsSealed: false } ? running.Id : null;
    }
}
