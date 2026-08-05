using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Models.Messages.Batches;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     Proves the Batch API path works, separately from the sweep.
///     <para>
///         Batching halves the bill and is the right choice in production — comments are not
///         latency-critical when the original renders immediately and the translations arrive
///         behind it. It is the wrong choice for a probe, where an hour-long round trip is an
///         hour per prompt revision, which is why the sweep runs synchronously and this stands
///         alone.
///     </para>
///     <para>
///         The discount is a billing rate, not a field on the response, so this cannot assert
///         it — what it can assert is that requests survive the round trip keyed by custom id,
///         which is the part that would actually break. Results come back in arbitrary order;
///         reading them positionally is the classic way to mistranslate every comment at once.
///     </para>
/// </summary>
public sealed class BatchTransportTests(ITestOutputHelper output)
{
    [TranslationProbeFact]
    public async Task ABatchRoundTripsKeyedByCustomId()
    {
        var client = new AnthropicClient { ApiKey = TranslationProbeConfiguration.ApiKey };

        var batch = await client.Messages.Batches.Create(new BatchCreateParams
        {
            Requests =
            [
                Item("ko", "Reply with only the word 채보."),
                Item("es", "Reply with only the word chart.")
            ]
        });

        output.WriteLine($"batch {batch.ID}: {batch.ProcessingStatus}");

        var deadline = TimeSpan.FromMinutes(10);
        var waited = TimeSpan.Zero;
        var poll = TimeSpan.FromSeconds(10);
        while (batch.ProcessingStatus != "ended" && waited < deadline)
        {
            await Task.Delay(poll);
            waited += poll;
            batch = await client.Messages.Batches.Retrieve(batch.ID);
        }

        // Compared through the implicit string conversion, not ToString(): the status wrapper
        // serializes itself, so ToString() hands back "ended" with the quotes still attached.
        Assert.True(batch.ProcessingStatus == "ended",
            $"Batch did not finish inside {deadline.TotalMinutes:F0} minutes.");

        var seen = new List<string>();
        await foreach (var result in client.Messages.Batches.ResultsStreaming(batch.ID))
        {
            seen.Add(result.CustomID);
            output.WriteLine($"{result.CustomID}: {result.Result}");
        }

        // Results arrive in arbitrary order. Reading them positionally is the classic way to
        // hand every comment somebody else's translation.
        Assert.Equal(["es", "ko"], seen.Order().ToArray());

        output.WriteLine($"waited {waited.TotalSeconds:F0}s for {batch.RequestCounts.Succeeded} succeeded, " +
                         $"{batch.RequestCounts.Errored} errored");
        output.WriteLine("The 50% discount is applied at billing and is not a field on any response.");

        Assert.Equal(2, (int)batch.RequestCounts.Succeeded);
    }

    private static Request Item(string id, string prompt)
    {
        return new Request
        {
            CustomID = id,
            Params = new Params
            {
                Model = ModelArm.Haiku.ModelId,
                MaxTokens = 32,
                Messages = [new MessageParam { Role = Role.User, Content = prompt }]
            }
        };
    }
}
