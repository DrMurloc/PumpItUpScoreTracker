using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     The boundary to a large language model. One request, one response, no conversation state —
///     everything the model needs rides in the prompts, so a caller can retry, batch, or re-point
///     a call at a different model without carrying history.
///     <para>
///         There is deliberately no implementation in <c>ScoreTracker.Data</c>: the only consumer
///         today is the translation workbench in <c>ScoreTracker.ExplorationTests</c>, which
///         supplies its own adapter. Nothing in the running application spends metered tokens.
///     </para>
/// </summary>
public interface ILanguageModelClient
{
    Task<LanguageModelResponse> Complete(LanguageModelRequest request, CancellationToken cancellationToken);
}
