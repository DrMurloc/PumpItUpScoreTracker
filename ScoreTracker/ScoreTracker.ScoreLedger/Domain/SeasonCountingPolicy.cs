using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     The counting rule (docs/design/seasons.md D15, §4.1) as a pure function: given one observed
///     score, does it belong to the season now running.
///     <para>
///         <b>Only the running season is ever writable</b> (D13, owner 2026-09-14: no grace period).
///         A season closes at its boundary, so a play you did not import before then is lost — which
///         is the whole rule, and the reason a board never moves after the quarter it belongs to has
///         finished.
///     </para>
///     <para>
///         Two facts about the official site decide what still counts. A <b>recently-played</b> play
///         is stamped with its own time, so one dated inside the running season belongs to it and one
///         dated earlier is an old score the site is only now showing us. A <b>best-list card</b> is
///         stamped with the chart's <i>first</i> play, so an in-season upscore can arrive wearing a
///         years-old date — which is why a card that raised a record we already held counts anyway:
///         the raise is something this import watched happen, and the only season it can belong to is
///         the one running. A brand-new importer's old cards raise nothing, so they stay out.
///     </para>
/// </summary>
internal static class SeasonCountingPolicy
{
    /// <summary>
    ///     The season to write, or null for "this is not a seasonal best".
    /// </summary>
    /// <param name="source">
    ///     The journal source. Only <c>officialImport</c> counts — the manual, CSV and API paths are
    ///     unverified and never seed a season's pool (D4).
    /// </param>
    /// <param name="observedAt">The play's own time, or the card's date when that is all there is.</param>
    /// <param name="raisedExistingRecord">
    ///     True only for a best-list card that raised an all-time record the player already held. An
    ///     observed play passes false: it is judged on its date alone.
    /// </param>
    /// <param name="seasons">Every season the roll has opened.</param>
    /// <param name="now">The clock. The season holding it is the only one that can be written.</param>
    public static SeasonId? SeasonFor(string source, DateTimeOffset observedAt, bool raisedExistingRecord,
        IReadOnlyList<SeasonRecord> seasons, DateTimeOffset now)
    {
        if (source != ScoreJournalEntry.OfficialImportSource) return null;

        var running = seasons.FirstOrDefault(s => s.Holds(now));
        if (running is not { IsSealed: false }) return null;

        return running.Holds(observedAt) || raisedExistingRecord ? running.Id : null;
    }
}
