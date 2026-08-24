using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     The boundary to a large language model's batch surface: many independent requests submitted
///     together, finishing asynchronously — most inside an hour, at most a day — at half the
///     per-token price. The sibling <see cref="ILanguageModelClient" /> is one synchronous call and
///     cannot express that shape, which is why this port exists rather than a flag on that one.
///     <para>
///         Results come back unordered and are correlated only by the caller's
///         <see cref="LanguageModelBatchItem.CustomId" />. Reading them positionally is the classic
///         way to hand every request somebody else's answer.
///     </para>
/// </summary>
public interface ILanguageModelBatchClient
{
    /// <summary>
    ///     Whether the adapter has credentials at all. A caller checks this before building work —
    ///     an unconfigured pipeline parks itself rather than submitting into an exception.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Submits one batch and returns the provider's id for it.</summary>
    Task<string> SubmitBatch(IReadOnlyList<LanguageModelBatchItem> items, CancellationToken cancellationToken);

    Task<LanguageModelBatchStatus> GetStatus(string batchId, CancellationToken cancellationToken);

    /// <summary>
    ///     Streams the results of a finished batch. Call only once <see cref="GetStatus" /> says
    ///     the batch has ended; every submitted item appears exactly once, keyed by its custom id.
    /// </summary>
    IAsyncEnumerable<LanguageModelBatchResult> GetResults(string batchId, CancellationToken cancellationToken);
}
