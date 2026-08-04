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

    public static AnthropicLanguageModelClient Create()
    {
        return new AnthropicLanguageModelClient(new AnthropicClient
        {
            ApiKey = TranslationProbeConfiguration.ApiKey
        });
    }

    public async Task<LanguageModelResponse> Complete(LanguageModelRequest request,
        CancellationToken cancellationToken)
    {
        var arm = ModelArm.For(request.ModelId);

        var parameters = new MessageCreateParams
        {
            Model = request.ModelId,
            MaxTokens = MaxTokens,
            System = new List<TextBlockParam> { new() { Text = request.SystemPrompt } },
            Messages = [new() { Role = Role.User, Content = request.UserPrompt }],
            // Opus 5 and Sonnet 5 think unless told not to, and thinking bills at the output
            // rate — for a two-sentence translation that is most of the cost and none of the
            // value. Haiku 4.5 does not think unless asked and rejects the effort parameter, so
            // it gets neither field.
            Thinking = arm.ThinksByDefault ? new ThinkingConfigDisabled() : null,
            OutputConfig = new OutputConfig
            {
                Effort = arm.SupportsEffort ? Effort.Medium : null,
                Format = request.JsonSchema == null
                    ? null
                    : new JsonOutputFormat
                    {
                        Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                            request.JsonSchema)!
                    }
            }
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
