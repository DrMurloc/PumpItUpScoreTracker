using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     The boundary to a large language model. One request, one response, no conversation state —
///     everything the model needs rides in the prompts, so a caller can retry, batch, or re-point
///     a call at a different model without carrying history.
///     <para>
///         This synchronous port still has no implementation in <c>ScoreTracker.Data</c> — its
///         only consumer is the translation workbench in <c>ScoreTracker.ExplorationTests</c>,
///         which supplies its own adapter. Production traffic goes through the batch sibling,
///         <see cref="ILanguageModelBatchClient" />, whose adapter ships but reports itself
///         unconfigured — and therefore inert — until a <c>ClaudeApi:ApiKey</c> is deliberately
///         supplied. Spending a metered token requires configuration, never just code.
///     </para>
/// </summary>
public interface ILanguageModelClient
{
    Task<LanguageModelResponse> Complete(LanguageModelRequest request, CancellationToken cancellationToken);
}
