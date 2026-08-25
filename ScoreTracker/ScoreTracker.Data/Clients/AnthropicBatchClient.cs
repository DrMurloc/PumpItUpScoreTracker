using System.Runtime.CompilerServices;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Models.Messages.Batches;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients
{
    /// <summary>
    ///     The Claude Batch API behind <see cref="ILanguageModelBatchClient" />. Batched requests
    ///     bill at half the synchronous rate and finish within a day; results are correlated by
    ///     custom id because they return unordered.
    ///     <para>
    ///         Thinking is disabled on every request — it bills at the output rate, and for a
    ///         translation-sized job it is most of the cost for none of the value. Disabling is
    ///         valid on every current model, unlike the effort field, so the client stays
    ///         model-agnostic and the model id remains the caller's choice.
    ///     </para>
    /// </summary>
    public sealed class AnthropicBatchClient : ILanguageModelBatchClient
    {
        /// <summary>
        ///     Headroom for a multi-locale JSON response where part of it is Korean, which
        ///     tokenizes far worse than Latin script.
        /// </summary>
        private const int MaxTokens = 4096;

        private readonly ClaudeApiConfiguration _configuration;

        public AnthropicBatchClient(IOptions<ClaudeApiConfiguration> options)
        {
            _configuration = options.Value;
        }

        public bool IsConfigured => _configuration.IsConfigured;

        public async Task<string> SubmitBatch(IReadOnlyList<LanguageModelBatchItem> items,
            CancellationToken cancellationToken)
        {
            var batch = await Client().Messages.Batches.Create(new BatchCreateParams
            {
                Requests = items.Select(ToRequest).ToArray()
            }, cancellationToken: cancellationToken);

            return batch.ID;
        }

        public async Task<LanguageModelBatchStatus> GetStatus(string batchId, CancellationToken cancellationToken)
        {
            var batch = await Client().Messages.Batches.Retrieve(batchId, cancellationToken: cancellationToken);

            // Compared through the implicit string conversion, not ToString(): the status wrapper
            // serializes itself, so ToString() hands back "ended" with the quotes still attached.
            return new LanguageModelBatchStatus(batch.ID,
                batch.ProcessingStatus == "ended",
                batch.RequestCounts.Succeeded,
                batch.RequestCounts.Errored,
                batch.RequestCounts.Expired,
                batch.RequestCounts.Canceled,
                batch.RequestCounts.Processing);
        }

        public async IAsyncEnumerable<LanguageModelBatchResult> GetResults(string batchId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in Client().Messages.Batches.ResultsStreaming(batchId,
                                   cancellationToken: cancellationToken)
                               .WithCancellation(cancellationToken))
                yield return ToResult(item);
        }

        private AnthropicClient Client()
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "No Claude API key is configured — check IsConfigured before submitting work.");

            return new AnthropicClient { ApiKey = _configuration.ApiKey };
        }

        private static Request ToRequest(LanguageModelBatchItem item)
        {
            var format = item.Request.JsonSchema == null
                ? null
                : new JsonOutputFormat
                {
                    Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.Request.JsonSchema)!
                };

            return new Request
            {
                CustomID = item.CustomId,
                Params = new Params
                {
                    Model = item.Request.ModelId,
                    MaxTokens = MaxTokens,
                    System = item.Request.SystemPrompt,
                    Messages = [new MessageParam { Role = Role.User, Content = item.Request.UserPrompt }],
                    Thinking = new ThinkingConfigDisabled(),
                    OutputConfig = format == null ? null : new OutputConfig { Format = format }
                }
            };
        }

        private static LanguageModelBatchResult ToResult(MessageBatchIndividualResponse item)
        {
            if (!item.Result.TryPickSucceeded(out var succeeded))
                return new LanguageModelBatchResult(item.CustomID, null, item.Result.Type.ToString());

            var message = succeeded.Message;
            if (message.StopReason?.ToString()?.Contains("refusal") == true)
                return new LanguageModelBatchResult(item.CustomID, null, "refusal");

            var text = string.Concat(message.Content
                .Select(block => block.Value)
                .OfType<TextBlock>()
                .Select(block => block.Text));

            return new LanguageModelBatchResult(item.CustomID, new LanguageModelResponse(text,
                message.Model.ToString() ?? item.CustomID,
                new LanguageModelUsage(
                    (int)message.Usage.InputTokens,
                    (int)message.Usage.OutputTokens,
                    (int)(message.Usage.CacheCreationInputTokens ?? 0),
                    (int)(message.Usage.CacheReadInputTokens ?? 0))));
        }
    }
}
