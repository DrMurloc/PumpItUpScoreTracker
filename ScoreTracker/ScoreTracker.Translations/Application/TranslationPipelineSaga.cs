using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Translations.Contracts;
using ScoreTracker.Translations.Contracts.Commands;
using ScoreTracker.Translations.Contracts.Events;
using ScoreTracker.Translations.Contracts.Messages;
using ScoreTracker.Translations.Contracts.Queries;
using ScoreTracker.Translations.Domain;
using ScoreTracker.Translations.Wiring;

namespace ScoreTracker.Translations.Application;

/// <summary>
///     The whole batch pipeline: queueing, the nightly submit, the hourly collect, and the admin
///     surface. One class because the pieces share every dependency and none is reachable except
///     through its message.
///     <para>
///         Money is controlled in one place — the submit step. It parks with no API key, checks
///         the rolling ceiling against recorded spend plus everything mid-pipeline, and takes at
///         most the nightly count of new texts. Work already pivoted is money already committed,
///         so it fans out without counting against a second night.
///     </para>
/// </summary>
internal sealed class TranslationPipelineSaga :
    IConsumer<QueueTextForTranslationCommand>,
    IConsumer<DiscardTranslationRequestsCommand>,
    IConsumer<SubmitTranslationBatchesCommand>,
    IConsumer<CollectTranslationBatchesCommand>,
    IRequestHandler<GetTranslationPipelineStatusQuery, TranslationPipelineStatusRecord>,
    IRequestHandler<GetRetranslationCostEstimateQuery, RetranslationEstimateRecord>,
    IRequestHandler<RetranslateAllCommand, int>
{
    /// <summary>
    ///     The entity's column width. A text longer than this cannot be stored, and no legitimate
    ///     caller produces one — the comment cap is 500 characters plus markers — so oversize is
    ///     logged and dropped rather than truncated into a text with half a marker in it.
    /// </summary>
    private const int MaxTextLength = 1000;

    private readonly ITranslationRequestRepository _requests;
    private readonly ITranslationBatchRepository _batches;
    private readonly ILanguageModelBatchClient _batchClient;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly TranslationsConfiguration _configuration;
    private readonly ILogger<TranslationPipelineSaga> _logger;

    public TranslationPipelineSaga(ITranslationRequestRepository requests, ITranslationBatchRepository batches,
        ILanguageModelBatchClient batchClient, IDateTimeOffsetAccessor clock,
        IOptions<TranslationsConfiguration> configuration, ILogger<TranslationPipelineSaga> logger)
    {
        _requests = requests;
        _batches = batches;
        _batchClient = batchClient;
        _clock = clock;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<QueueTextForTranslationCommand> context)
    {
        if (context.Message.Text.Length > MaxTextLength)
        {
            _logger.LogWarning("Refusing to queue {SourceKey}: {Length} characters is over the pipeline's cap",
                context.Message.SourceKey, context.Message.Text.Length);
            return;
        }

        await _requests.Upsert(context.Message.SourceKey, context.Message.Text, _clock.Now,
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<DiscardTranslationRequestsCommand> context)
    {
        if (context.Message.SourceKeys.Count == 0) return;

        await _requests.Discard(context.Message.SourceKeys, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<SubmitTranslationBatchesCommand> context)
    {
        if (!_batchClient.IsConfigured)
        {
            _logger.LogInformation("Translation submit is parked: no ClaudeApi:ApiKey is configured");
            return;
        }

        var now = _clock.Now;

        // Fan-out first, and outside the allowance: a pivoted text is money already committed —
        // its night was the night its pivot was allowed in — and holding it hostage to tonight's
        // budget would strand half-translated work behind new work.
        var pivoted = await _requests.NextIn(TranslationState.PivotDone, _configuration.NightlyCount,
            context.CancellationToken);
        if (pivoted.Count > 0)
            await SubmitStage(pivoted, TranslationState.FanOutSubmitted, ToFanOutItem, now,
                context.CancellationToken);

        var allowance = TranslationBudget.Allowance(
            _configuration.MonthlyCeilingUsd,
            await _batches.SpendSince(now.AddDays(-30), context.CancellationToken),
            await InFlightEstimate(context.CancellationToken),
            _configuration.EstimatedCostPerTextUsd,
            _configuration.NightlyCount);

        if (allowance <= 0)
        {
            if (await _requests.CountIn(TranslationState.Pending, context.CancellationToken) > 0)
                _logger.LogWarning(
                    "The translation ceiling parked tonight's submit with texts still pending — " +
                    "the fuse is doing its job, but somebody should look at why");
            return;
        }

        var pending = await _requests.NextIn(TranslationState.Pending, allowance, context.CancellationToken);
        if (pending.Count > 0)
            await SubmitStage(pending, TranslationState.PivotSubmitted, ToPivotItem, now, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<CollectTranslationBatchesCommand> context)
    {
        foreach (var batch in await _batches.Open(context.CancellationToken))
        {
            var status = await _batchClient.GetStatus(batch.ProviderBatchId, context.CancellationToken);
            if (!status.HasEnded) continue;

            await CollectBatch(batch, context);
        }
    }

    public async Task<TranslationPipelineStatusRecord> Handle(GetTranslationPipelineStatusQuery request,
        CancellationToken cancellationToken)
    {
        var inFlight = await _requests.CountIn(TranslationState.PivotSubmitted, cancellationToken)
                       + await _requests.CountIn(TranslationState.FanOutSubmitted, cancellationToken);
        var failures = await _requests.RecentFailures(10, cancellationToken);

        return new TranslationPipelineStatusRecord(
            _batchClient.IsConfigured,
            await _requests.CountIn(TranslationState.Pending, cancellationToken),
            inFlight,
            await _requests.CountIn(TranslationState.PivotDone, cancellationToken),
            await _requests.CountIn(TranslationState.Translated, cancellationToken),
            await _requests.CountIn(TranslationState.Failed, cancellationToken),
            await _requests.OldestPendingCreatedAt(cancellationToken),
            await _batches.SpendSince(_clock.Now.AddDays(-30), cancellationToken),
            await InFlightEstimate(cancellationToken),
            _configuration.MonthlyCeilingUsd,
            _configuration.NightlyCount,
            await _batches.LastSubmittedAt(cancellationToken),
            await _batches.LastCollectedAt(cancellationToken),
            failures.Select(f => new TranslationFailureRecord(f.SourceKey,
                f.FailureReason ?? string.Empty, f.UpdatedAt)).ToArray());
    }

    public async Task<RetranslationEstimateRecord> Handle(GetRetranslationCostEstimateQuery request,
        CancellationToken cancellationToken)
    {
        var count = await _requests.CountIn(TranslationState.Translated, cancellationToken);

        return new RetranslationEstimateRecord(count, count * _configuration.EstimatedCostPerTextUsd);
    }

    public async Task<int> Handle(RetranslateAllCommand request, CancellationToken cancellationToken)
    {
        return await _requests.RequeueTranslated(_clock.Now, cancellationToken);
    }

    /// <summary>
    ///     Everything mid-pipeline, priced at the full per-text estimate. Deliberately
    ///     pessimistic — a PivotDone text has only its fan-out left, but the ceiling is a fuse,
    ///     and a fuse that under-counts committed money is a fuse that fires late.
    /// </summary>
    private async Task<decimal> InFlightEstimate(CancellationToken cancellationToken)
    {
        var count = await _requests.CountIn(TranslationState.PivotSubmitted, cancellationToken)
                    + await _requests.CountIn(TranslationState.PivotDone, cancellationToken)
                    + await _requests.CountIn(TranslationState.FanOutSubmitted, cancellationToken);

        return count * _configuration.EstimatedCostPerTextUsd;
    }

    private async Task SubmitStage(IReadOnlyList<TranslationWork> works, TranslationState submittedState,
        Func<TranslationWork, LanguageModelBatchItem?> toItem, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var items = new List<LanguageModelBatchItem>();
        var ids = new List<Guid>();
        foreach (var work in works)
        {
            var item = toItem(work);
            if (item == null) continue;

            items.Add(item);
            ids.Add(work.Id);
        }

        if (items.Count == 0) return;

        // Submit before recording: a crash in the gap leaves an orphaned provider batch (bounded
        // by one night's count) rather than rows marked submitted into a batch that never went
        // out — which would never collect and never retry.
        var providerBatchId = await _batchClient.SubmitBatch(items, cancellationToken);
        var batch = new TranslationBatchInfo(Guid.NewGuid(), providerBatchId, submittedState, now);
        await _batches.Record(batch, items.Count, cancellationToken);
        await _requests.MarkSubmitted(ids, batch.Id, submittedState, now, cancellationToken);

        _logger.LogInformation("Submitted {Count} texts as {Stage} batch {ProviderBatchId}",
            items.Count, submittedState, providerBatchId);
    }

    private LanguageModelBatchItem ToPivotItem(TranslationWork work)
    {
        return new LanguageModelBatchItem(work.Id.ToString("N"), new LanguageModelRequest(
            _configuration.PivotModelId,
            PivotPrompt.System(),
            PivotPrompt.User(work.Text),
            PivotPrompt.Schema));
    }

    private LanguageModelBatchItem? ToFanOutItem(TranslationWork work)
    {
        if (work.PivotJson == null || work.SourceLanguage == null) return null;

        var targets = TranslationTarget.ForSource(work.SourceLanguage);
        var pivot = TranslationResponseReader.ReadPivot(work.PivotJson);

        return new LanguageModelBatchItem(work.Id.ToString("N"), new LanguageModelRequest(
            _configuration.FanOutModelId,
            FanOutPrompt.System(targets),
            FanOutPrompt.User(PivotPrompt.Render(pivot)),
            FanOutPrompt.Schema(targets)));
    }

    private async Task CollectBatch(TranslationBatchInfo batch, ConsumeContext context)
    {
        var byCustomId = (await _requests.InBatch(batch.Id, context.CancellationToken))
            .ToDictionary(w => w.Id.ToString("N"));
        var usage = new LanguageModelUsage(0, 0);
        var now = _clock.Now;

        await foreach (var result in _batchClient.GetResults(batch.ProviderBatchId, context.CancellationToken))
        {
            // A text re-queued or discarded while its batch was in flight no longer points here;
            // its result is paid for and ignored, which is the cheap side of that race.
            if (!byCustomId.TryGetValue(result.CustomId, out var work)) continue;

            if (result.Response == null)
            {
                await _requests.Fail(work.Id, $"the model returned {result.Error}", now, context.CancellationToken);
                continue;
            }

            usage = Add(usage, result.Response.Usage);

            try
            {
                if (batch.Stage == TranslationState.PivotSubmitted)
                    await CollectPivot(work, result.Response.Text, now, context.CancellationToken);
                else
                    await CollectFanOut(work, result.Response.Text, now, context);
            }
            catch (Exception exception)
            {
                // A malformed response fails its own text, never the batch around it — the other
                // forty-nine results are real money already spent.
                await _requests.Fail(work.Id, exception.Message, now, context.CancellationToken);
            }
        }

        await _batches.Complete(batch.Id, usage,
            TranslationBudget.Cost(usage, _configuration.InputPerMillionUsd, _configuration.OutputPerMillionUsd),
            now, context.CancellationToken);
    }

    private async Task CollectPivot(TranslationWork work, string responseText, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pivot = TranslationResponseReader.ReadPivot(responseText);

        // The fan-out translates from the pivot, so a pivot that mishandled a marker has already
        // lost the link — there is nothing downstream worth paying for.
        var violation = TranslationMarkers.Violation(work.Text, pivot.English);
        if (violation != null)
        {
            await _requests.Fail(work.Id, $"the pivot {violation}", now, cancellationToken);
            return;
        }

        await _requests.CompletePivot(work.Id, pivot.SourceLanguage, responseText, now, cancellationToken);
    }

    private async Task CollectFanOut(TranslationWork work, string responseText, DateTimeOffset now,
        ConsumeContext context)
    {
        var pivot = TranslationResponseReader.ReadPivot(work.PivotJson!);
        var targets = TranslationTarget.ForSource(work.SourceLanguage);
        var kept = new Dictionary<string, string>();

        foreach (var (locale, rendered) in TranslationResponseReader.ReadTranslations(responseText))
        {
            // The schema constrains locales to the request's targets; this is the belt for a
            // response that arrived without one.
            if (!targets.Contains(locale)) continue;

            var violation = TranslationMarkers.Violation(work.Text, rendered);
            if (violation != null)
            {
                _logger.LogWarning("Discarding the {Locale} rendering of {SourceKey}: it {Violation}",
                    locale, work.SourceKey, violation);
                continue;
            }

            kept[locale] = rendered;
        }

        // The pivot is the English rendering — already marker-verified at its own stage. Absent
        // when the author wrote English, because their words are already on the page.
        if (!TranslationTarget.SharesLanguage(TranslationTarget.Pivot, pivot.SourceLanguage))
            kept[TranslationTarget.Pivot] = pivot.English;

        if (kept.Count == 0)
        {
            await _requests.Fail(work.Id, "every rendering failed the marker check", now, context.CancellationToken);
            return;
        }

        await _requests.CompleteTranslation(work.Id, now, context.CancellationToken);
        await context.Publish(new TextTranslatedEvent(work.SourceKey, work.Text, pivot.SourceLanguage, kept,
            $"{_configuration.PivotModelId}+{_configuration.FanOutModelId} via {TranslationTarget.Pivot} pivot"));
    }

    private static LanguageModelUsage Add(LanguageModelUsage total, LanguageModelUsage call)
    {
        return new LanguageModelUsage(
            total.InputTokens + call.InputTokens,
            total.OutputTokens + call.OutputTokens,
            total.CacheCreationInputTokens + call.CacheCreationInputTokens,
            total.CacheReadInputTokens + call.CacheReadInputTokens);
    }
}
