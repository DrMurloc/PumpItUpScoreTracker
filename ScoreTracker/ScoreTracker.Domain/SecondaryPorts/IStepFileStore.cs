namespace ScoreTracker.Domain.SecondaryPorts
{
    /// <summary>
    ///     Custody of the raw community step files (docs/design/step-chart-failure-map.md D7):
    ///     the .ssc sources an ingestion carried, banked vintage-by-vintage so re-analysis is a
    ///     button reading the archive instead of a re-upload — and so the corpus is held at all,
    ///     the lesson piucenter's wind-down taught.
    ///     <para>
    ///         Deliberately optional: when <see cref="IsConfigured" /> is false (no blob secret —
    ///         local dev, CI, E2E) every caller skips the archive and the feature still runs
    ///         whole, because ingest parses the uploaded zip directly and never reads back
    ///         through this port on a page view.
    ///     </para>
    /// </summary>
    public interface IStepFileStore
    {
        bool IsConfigured { get; }

        /// <summary>Archives one file under the vintage. Overwrites — save semantics.</summary>
        Task Put(string vintage, string path, Stream content, CancellationToken cancellationToken = default);

        /// <summary>The archived file's text, or null when the vintage never banked it.</summary>
        Task<string?> GetText(string vintage, string path, CancellationToken cancellationToken = default);

        /// <summary>Every archived path under one vintage.</summary>
        Task<IReadOnlyList<string>> List(string vintage, CancellationToken cancellationToken = default);

        /// <summary>The banked vintages, so a reprocess can find the newest without an upload.</summary>
        Task<IReadOnlyList<string>> ListVintages(CancellationToken cancellationToken = default);
    }
}
