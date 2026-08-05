using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     The only implementation of <see cref="ILanguageModelClient" /> anywhere in the solution,
///     and it lives in the manual-only workbench on purpose: nothing that ships can reach a
///     metered API.
///     <para>
///         Synchronous, one call per request. The Batch API halves the bill and is the right
///         choice in production, but a probe whose feedback loop is an hour long is a probe
///         nobody iterates on — <see cref="BatchTransportTests" /> proves that path separately.
///     </para>
/// </summary>
internal sealed class AnthropicLanguageModelClient(AnthropicClient client) : ILanguageModelClient
{
    /// <summary>
    ///     Room for the fan-out's four renderings plus JSON, with headroom for Korean — which
    ///     tokenizes far worse than Latin script, so a limit that fits English comfortably can
    ///     truncate the same sentence in Hangul.
    /// </summary>
    private const int MaxTokens = 4096;

    /// <summary>
    ///     How long a request gets before it is treated as wedged rather than slow.
    ///     <para>
    ///         The SDK's ten-minute default is the wrong shape for a bounded-concurrency sweep: a
    ///         few stuck requests hold every slot for ten minutes each and stall the run behind
    ///         them. A first attempt at ninety seconds then proved too tight in the other
    ///         direction — Opus and Sonnet exceed it under load while Haiku, being faster, never
    ///         did, so the cap was manufacturing failures for exactly the two arms being compared.
    ///         Three minutes clears observed latency with room for one backoff, and still frees a
    ///         slot long before the default would.
    ///     </para>
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(3);

    public static AnthropicLanguageModelClient Create()
    {
        return new AnthropicLanguageModelClient(new AnthropicClient
        {
            ApiKey = TranslationProbeConfiguration.ApiKey,
            Timeout = RequestTimeout
        });
    }

    public async Task<LanguageModelResponse> Complete(LanguageModelRequest request,
        CancellationToken cancellationToken)
    {
        var arm = ModelArm.For(request.ModelId);
        var system = new List<TextBlockParam> { new() { Text = request.SystemPrompt } };
        List<MessageParam> messages = [new() { Role = Role.User, Content = request.UserPrompt }];
        var format = request.JsonSchema == null
            ? null
            : new JsonOutputFormat
            {
                Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.JsonSchema)!
            };

        // The two shapes are spelled out rather than nulled into one, because the API rejects an
        // explicit null as hard as a wrong value — "thinking: Input should be an object" is what
        // a `Thinking = null` earns on a model that simply does not take the field. Omission and
        // null are different requests.
        //
        // Opus 5 and Sonnet 5 think unless told not to, and thinking bills at the output rate:
        // for a two-sentence translation that is most of the cost and none of the value. Haiku
        // 4.5 does not think unless asked and rejects effort outright, so it gets neither field.
        var parameters = arm.ThinksByDefault
            ? new MessageCreateParams
            {
                Model = request.ModelId,
                MaxTokens = MaxTokens,
                System = system,
                Messages = messages,
                Thinking = new ThinkingConfigDisabled(),
                OutputConfig = new OutputConfig { Effort = Effort.Medium, Format = format }
            }
            : new MessageCreateParams
            {
                Model = request.ModelId,
                MaxTokens = MaxTokens,
                System = system,
                Messages = messages,
                OutputConfig = new OutputConfig { Format = format }
            };

        var response = await client.Messages.Create(parameters, cancellationToken: cancellationToken);

        if (response.StopReason == "refusal")
            throw new InvalidOperationException(
                $"'{request.ModelId}' declined the request. Nothing was translated.");

        var text = string.Concat(response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text));

        return new LanguageModelResponse(text, response.Model.ToString() ?? request.ModelId,
            new LanguageModelUsage(
                (int)response.Usage.InputTokens,
                (int)response.Usage.OutputTokens,
                (int)(response.Usage.CacheCreationInputTokens ?? 0),
                (int)(response.Usage.CacheReadInputTokens ?? 0)));
    }
}
