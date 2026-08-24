using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestHelpers;
using ScoreTracker.Translations.Application;
using ScoreTracker.Translations.Contracts.Commands;
using ScoreTracker.Translations.Contracts.Events;
using ScoreTracker.Translations.Contracts.Messages;
using ScoreTracker.Translations.Contracts.Queries;
using ScoreTracker.Translations.Domain;
using ScoreTracker.Translations.Wiring;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TranslationPipelineSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 6, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITranslationRequestRepository> _requests = new();
    private readonly Mock<ITranslationBatchRepository> _batches = new();
    private readonly Mock<ILanguageModelBatchClient> _client = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly TranslationsConfiguration _configuration = new();

    /// <summary>The site admin, whose id User.IsAdmin computes against — no flag, no seed row.</summary>
    private static readonly ScoreTracker.Domain.Models.User Admin = new(
        Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"),
        ScoreTracker.SharedKernel.ValueTypes.Name.From("DrMurloc"), true, null,
        new Uri("https://example.com/d.png"), ScoreTracker.SharedKernel.ValueTypes.Name.From("US"));

    public TranslationPipelineSagaTests()
    {
        _client.SetupGet(c => c.IsConfigured).Returns(true);
        _client.Setup(c => c.SubmitBatch(It.IsAny<IReadOnlyList<LanguageModelBatchItem>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync("provider-batch-1");
        _requests.Setup(r => r.NextIn(It.IsAny<TranslationState>(), It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TranslationWork>());
        _requests.Setup(r => r.CountIn(It.IsAny<TranslationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _batches.Setup(b => b.SpendSince(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _batches.Setup(b => b.Open(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TranslationBatchInfo>());
        _requests.Setup(r => r.RecentFailures(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TranslationWork>());
        _requests.Setup(r => r.MarkSubmitted(It.IsAny<IReadOnlyList<TranslationWork>>(), It.IsAny<Guid>(),
                It.IsAny<TranslationState>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TranslationWork> works, Guid _, TranslationState _, DateTimeOffset _,
                CancellationToken _) => works.Select(w => w.Id).ToArray());
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(Admin);
    }

    private TranslationPipelineSaga Saga()
    {
        return new TranslationPipelineSaga(_requests.Object, _batches.Object, _client.Object,
            FakeDateTime.At(Now).Object, _currentUser.Object, Options.Create(_configuration),
            NullLogger<TranslationPipelineSaga>.Instance);
    }

    private static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        return context.Object;
    }

    private static TranslationWork Work(string sourceKey, string text,
        TranslationState state = TranslationState.Pending, string? sourceLanguage = null, string? pivotJson = null)
    {
        return new TranslationWork(Guid.NewGuid(), sourceKey, text, state, sourceLanguage, pivotJson,
            null, Now.AddDays(-1), Now.AddDays(-1));
    }

    private static string PivotJson(string english, string sourceLanguage = "ko")
    {
        return $$"""
                 {"source_language":"{{sourceLanguage}}","english":{{System.Text.Json.JsonSerializer.Serialize(english)}},"register":"casual","formality_marked":true,"tone":"friendly advice","entities":[]}
                 """;
    }

    private static async IAsyncEnumerable<LanguageModelBatchResult> Results(
        params LanguageModelBatchResult[] results)
    {
        foreach (var result in results) yield return result;
        await Task.CompletedTask;
    }

    [Fact]
    public async Task QueueingUpsertsTheTextAtTheCurrentClock()
    {
        await Saga().Consume(ContextFor(new QueueTextForTranslationCommand("chart-comment:abc", "hola ⟦1⟧")));

        _requests.Verify(r => r.Upsert("chart-comment:abc", "hola ⟦1⟧", Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OversizeTextIsRefusedRatherThanTruncatedIntoHalfAMarker()
    {
        await Saga().Consume(ContextFor(new QueueTextForTranslationCommand("k", new string('a', 1001))));

        _requests.Verify(r => r.Upsert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DiscardDropsTheNamedKeys()
    {
        var keys = new[] { "chart-comment:a", "chart-comment:b" };

        await Saga().Consume(ContextFor(new DiscardTranslationRequestsCommand(keys)));

        _requests.Verify(r => r.Discard(keys, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitParksEntirelyWithoutAnApiKey()
    {
        _client.SetupGet(c => c.IsConfigured).Returns(false);

        await Saga().Consume(ContextFor(new SubmitTranslationBatchesCommand()));

        _requests.Verify(r => r.NextIn(It.IsAny<TranslationState>(), It.IsAny<int>(),
            It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        _client.Verify(c => c.SubmitBatch(It.IsAny<IReadOnlyList<LanguageModelBatchItem>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitSendsPendingTextsAsAPivotBatchAndMarksThem()
    {
        var first = Work("chart-comment:a", "check ⟦1⟧ out");
        var second = Work("chart-comment:b", "FATALITY");
        _requests.Setup(r => r.NextIn(TranslationState.Pending, It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });
        IReadOnlyList<LanguageModelBatchItem>? submitted = null;
        _client.Setup(c => c.SubmitBatch(It.IsAny<IReadOnlyList<LanguageModelBatchItem>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<LanguageModelBatchItem>, CancellationToken>((items, _) => submitted = items)
            .ReturnsAsync("provider-batch-1");

        await Saga().Consume(ContextFor(new SubmitTranslationBatchesCommand()));

        Assert.NotNull(submitted);
        Assert.Equal(new[] { first.Id.ToString("N"), second.Id.ToString("N") },
            submitted!.Select(i => i.CustomId).ToArray());
        Assert.All(submitted, i => Assert.Equal("claude-sonnet-5", i.Request.ModelId));
        Assert.Contains("Link markers", submitted[0].Request.SystemPrompt);
        _batches.Verify(b => b.Record(It.Is<TranslationBatchInfo>(info =>
                info.ProviderBatchId == "provider-batch-1" && info.Stage == TranslationState.PivotSubmitted),
            2, It.IsAny<CancellationToken>()), Times.Once);
        _requests.Verify(r => r.MarkSubmitted(
            It.Is<IReadOnlyList<TranslationWork>>(w => w.Select(x => x.Id).SequenceEqual(new[] { first.Id, second.Id })),
            It.IsAny<Guid>(), TranslationState.PivotSubmitted, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheAllowanceIsHeadroomOverThePerTextEstimate()
    {
        // $0.20 of headroom at the default $0.016 estimate is twelve texts, floored.
        _batches.Setup(b => b.SpendSince(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(29.80m);

        await Saga().Consume(ContextFor(new SubmitTranslationBatchesCommand()));

        // The cooldown cutoff rides every pivot intake: a text enters a batch at most once per 24h.
        _requests.Verify(r => r.NextIn(TranslationState.Pending, 12,
            Now - TranslationPipelineSaga.SubmitCooldown, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MidPipelineWorkCountsAgainstTheCeilingAsCommittedMoney()
    {
        _batches.Setup(b => b.SpendSince(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(29.80m);
        _requests.Setup(r => r.CountIn(TranslationState.PivotSubmitted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        await Saga().Consume(ContextFor(new SubmitTranslationBatchesCommand()));

        // Ten in flight at $0.016 eats $0.16 of the $0.20 headroom, leaving room for two.
        _requests.Verify(r => r.NextIn(TranslationState.Pending, 2,
            It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABlownCeilingParksNewWorkButNeverStrandsPivotedWork()
    {
        _batches.Setup(b => b.SpendSince(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30m);
        var pivoted = Work("chart-comment:a", "hola ⟦1⟧", TranslationState.PivotDone, "ko",
            PivotJson("hi ⟦1⟧"));
        _requests.Setup(r => r.NextIn(TranslationState.PivotDone, It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pivoted });

        await Saga().Consume(ContextFor(new SubmitTranslationBatchesCommand()));

        _client.Verify(c => c.SubmitBatch(It.IsAny<IReadOnlyList<LanguageModelBatchItem>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _requests.Verify(r => r.NextIn(TranslationState.Pending, It.IsAny<int>(),
            It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFanOutItemTargetsEverythingButTheSourceLanguage()
    {
        var pivoted = Work("chart-comment:a", "안녕 ⟦1⟧", TranslationState.PivotDone, "ko",
            PivotJson("hi ⟦1⟧"));
        _requests.Setup(r => r.NextIn(TranslationState.PivotDone, It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pivoted });
        IReadOnlyList<LanguageModelBatchItem>? submitted = null;
        _client.Setup(c => c.SubmitBatch(It.IsAny<IReadOnlyList<LanguageModelBatchItem>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<LanguageModelBatchItem>, CancellationToken>((items, _) => submitted = items)
            .ReturnsAsync("provider-batch-2");

        await Saga().Consume(ContextFor(new SubmitTranslationBatchesCommand()));

        var schema = Assert.Single(submitted!).Request.JsonSchema!;
        Assert.Contains("es-ES", schema);
        Assert.DoesNotContain("ko-KR", schema);
    }

    [Fact]
    public async Task CollectLeavesARunningBatchAlone()
    {
        var batch = new TranslationBatchInfo(Guid.NewGuid(), "pb", TranslationState.PivotSubmitted, Now);
        _batches.Setup(b => b.Open(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { batch });
        _client.Setup(c => c.GetStatus("pb", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LanguageModelBatchStatus("pb", false, 0, 0, 0, 0, 2));

        await Saga().Consume(ContextFor(new CollectTranslationBatchesCommand()));

        _client.Verify(c => c.GetResults(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private (TranslationBatchInfo Batch, TranslationWork Work) EndedBatch(TranslationState stage,
        TranslationWork work)
    {
        var batch = new TranslationBatchInfo(Guid.NewGuid(), "pb", stage, Now.AddHours(-2));
        _batches.Setup(b => b.Open(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { batch });
        _client.Setup(c => c.GetStatus("pb", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LanguageModelBatchStatus("pb", true, 1, 0, 0, 0, 0));
        _requests.Setup(r => r.InBatch(batch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { work });

        return (batch, work);
    }

    [Fact]
    public async Task AFinishedPivotIsStoredWholeWithItsDetectedLanguage()
    {
        var work = Work("chart-comment:a", "안녕 ⟦1⟧", TranslationState.PivotSubmitted);
        var (batch, _) = EndedBatch(TranslationState.PivotSubmitted, work);
        var json = PivotJson("hi ⟦1⟧");
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"),
                new LanguageModelResponse(json, "claude-sonnet-5", new LanguageModelUsage(100, 50)))));

        await Saga().Consume(ContextFor(new CollectTranslationBatchesCommand()));

        _requests.Verify(r => r.CompletePivot(work.Id, "ko", json, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _batches.Verify(b => b.Complete(batch.Id,
            It.Is<LanguageModelUsage>(u => u.InputTokens == 100 && u.OutputTokens == 50),
            It.Is<decimal>(cost => cost > 0), Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APivotThatLosesAMarkerFailsItsTextBeforeMoneyGoesDownstream()
    {
        var work = Work("chart-comment:a", "안녕 ⟦1⟧", TranslationState.PivotSubmitted);
        EndedBatch(TranslationState.PivotSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"),
                new LanguageModelResponse(PivotJson("hi there"), "m", new LanguageModelUsage(1, 1)))));

        await Saga().Consume(ContextFor(new CollectTranslationBatchesCommand()));

        _requests.Verify(r => r.Fail(work.Id, It.Is<string>(reason => reason.StartsWith("the pivot ") && reason.Contains("marker")),
            Now, It.IsAny<CancellationToken>()), Times.Once);
        _requests.Verify(r => r.CompletePivot(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static string FanOutJson(params (string Locale, string Text)[] translations)
    {
        var items = string.Join(",", translations.Select(t =>
            $$"""{"locale":"{{t.Locale}}","text":{{System.Text.Json.JsonSerializer.Serialize(t.Text)}}}"""));

        return $$"""{"translations":[{{items}}]}""";
    }

    [Fact]
    public async Task AFinishedFanOutPublishesRenderingsWithThePivotAsEnglish()
    {
        var work = Work("chart-comment:a", "안녕 ⟦1⟧", TranslationState.FanOutSubmitted, "ko",
            PivotJson("hi ⟦1⟧"));
        var (_, _) = EndedBatch(TranslationState.FanOutSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"),
                new LanguageModelResponse(
                    FanOutJson(("es-ES", "hola ⟦1⟧"), ("fr-FR", "salut ⟦1⟧"), ("pt-BR", "oi ⟦1⟧")),
                    "m", new LanguageModelUsage(1, 1)))));
        var context = ContextFor(new CollectTranslationBatchesCommand());

        await Saga().Consume(context);

        _requests.Verify(r => r.CompleteTranslation(work.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
        Mock.Get(context).Verify(c => c.Publish(It.Is<TextTranslatedEvent>(e =>
                e.SourceKey == "chart-comment:a"
                && e.SourceLanguage == "ko"
                && e.Translations.Count == 4
                && e.Translations["en-US"] == "hi ⟦1⟧"
                && e.Translations["es-ES"] == "hola ⟦1⟧"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnEnglishSourceNeverGainsAnEnglishRendering()
    {
        var work = Work("chart-comment:a", "nice ⟦1⟧", TranslationState.FanOutSubmitted, "en",
            PivotJson("nice ⟦1⟧", "en"));
        EndedBatch(TranslationState.FanOutSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"),
                new LanguageModelResponse(
                    FanOutJson(("es-ES", "genial ⟦1⟧"), ("fr-FR", "super ⟦1⟧"), ("ko-KR", "좋다 ⟦1⟧"),
                        ("pt-BR", "legal ⟦1⟧")),
                    "m", new LanguageModelUsage(1, 1)))));
        var context = ContextFor(new CollectTranslationBatchesCommand());

        await Saga().Consume(context);

        Mock.Get(context).Verify(c => c.Publish(It.Is<TextTranslatedEvent>(e =>
                e.Translations.Count == 4 && !e.Translations.ContainsKey("en-US")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARenderingThatInventsALinkIsDiscardedAloneNotTheWholeText()
    {
        var work = Work("chart-comment:a", "안녕 ⟦1⟧", TranslationState.FanOutSubmitted, "ko",
            PivotJson("hi ⟦1⟧"));
        EndedBatch(TranslationState.FanOutSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"),
                new LanguageModelResponse(
                    FanOutJson(("es-ES", "hola ⟦1⟧ https://evil.example"), ("fr-FR", "salut ⟦1⟧")),
                    "m", new LanguageModelUsage(1, 1)))));
        var context = ContextFor(new CollectTranslationBatchesCommand());

        await Saga().Consume(context);

        Mock.Get(context).Verify(c => c.Publish(It.Is<TextTranslatedEvent>(e =>
                !e.Translations.ContainsKey("es-ES") && e.Translations.ContainsKey("fr-FR")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EveryRenderingFailingFailsTheTextInsteadOfPublishingNothing()
    {
        var work = Work("chart-comment:a", "nice ⟦1⟧", TranslationState.FanOutSubmitted, "en",
            PivotJson("nice ⟦1⟧", "en"));
        EndedBatch(TranslationState.FanOutSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"),
                new LanguageModelResponse(FanOutJson(("es-ES", "genial, sin marcador")),
                    "m", new LanguageModelUsage(1, 1)))));
        var context = ContextFor(new CollectTranslationBatchesCommand());

        await Saga().Consume(context);

        _requests.Verify(r => r.Fail(work.Id, "every rendering failed the marker check", Now,
            It.IsAny<CancellationToken>()), Times.Once);
        Mock.Get(context).Verify(c => c.Publish(It.IsAny<TextTranslatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnErroredItemFailsItsOwnTextAndTheBatchStillCloses()
    {
        var work = Work("chart-comment:a", "안녕", TranslationState.PivotSubmitted);
        var (batch, _) = EndedBatch(TranslationState.PivotSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"), null, "errored")));

        await Saga().Consume(ContextFor(new CollectTranslationBatchesCommand()));

        _requests.Verify(r => r.Fail(work.Id, "the model returned errored", Now,
            It.IsAny<CancellationToken>()), Times.Once);
        _batches.Verify(b => b.Complete(batch.Id, It.IsAny<LanguageModelUsage>(), 0m, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AResultForAReplacedTextIsIgnoredNotMisapplied()
    {
        var work = Work("chart-comment:a", "안녕", TranslationState.PivotSubmitted);
        EndedBatch(TranslationState.PivotSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(Guid.NewGuid().ToString("N"),
                new LanguageModelResponse(PivotJson("hi"), "m", new LanguageModelUsage(1, 1)))));

        await Saga().Consume(ContextFor(new CollectTranslationBatchesCommand()));

        _requests.Verify(r => r.CompletePivot(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _requests.Verify(r => r.Fail(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        // Paid for and ignored — but RECORDED: the fuse reads this ledger, and an under-counting
        // fuse fires late.
        _batches.Verify(b => b.Complete(It.IsAny<Guid>(),
            It.Is<LanguageModelUsage>(u => u.InputTokens == 1 && u.OutputTokens == 1),
            It.IsAny<decimal>(), Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailureIsAnnouncedSoTheBadgeCanStopPromising()
    {
        var work = Work("chart-comment:a", "안녕", TranslationState.PivotSubmitted);
        EndedBatch(TranslationState.PivotSubmitted, work);
        _client.Setup(c => c.GetResults("pb", It.IsAny<CancellationToken>()))
            .Returns(Results(new LanguageModelBatchResult(work.Id.ToString("N"), null, "errored")));
        var context = ContextFor(new CollectTranslationBatchesCommand());

        await Saga().Consume(context);

        Mock.Get(context).Verify(c => c.Publish(It.Is<TextTranslationFailedEvent>(e =>
            e.SourceKey == "chart-comment:a"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectParksWithoutAnApiKeyInsteadOfFaultingHourly()
    {
        _client.SetupGet(c => c.IsConfigured).Returns(false);
        _batches.Setup(b => b.Open(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TranslationBatchInfo(Guid.NewGuid(), "pb", TranslationState.PivotSubmitted, Now) });

        await Saga().Consume(ContextFor(new CollectTranslationBatchesCommand()));

        _client.Verify(c => c.GetStatus(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryFailedRequeuesAndReportsTheCount()
    {
        _requests.Setup(r => r.RequeueFailed(Now, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        Assert.Equal(4, await Saga().Handle(new RetryFailedTranslationsCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task TheMoneyLeversAreTheSiteAdminsAlone()
    {
        _currentUser.SetupGet(c => c.User).Returns(new ScoreTracker.Domain.Models.User(Guid.NewGuid(),
            ScoreTracker.SharedKernel.ValueTypes.Name.From("NotAdmin"), true, null,
            new Uri("https://example.com/n.png"), ScoreTracker.SharedKernel.ValueTypes.Name.From("US")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Saga().Handle(new RetranslateAllCommand(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Saga().Handle(new RetryFailedTranslationsCommand(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Saga().Handle(new GetTranslationPipelineStatusQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task RetranslateAllRequeuesAndReportsTheCount()
    {
        _requests.Setup(r => r.RequeueTranslated(Now, It.IsAny<CancellationToken>())).ReturnsAsync(217);

        Assert.Equal(217, await Saga().Handle(new RetranslateAllCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task TheRetranslationQuoteIsCountTimesTheEstimate()
    {
        _requests.Setup(r => r.CountIn(TranslationState.Translated, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200);

        var estimate = await Saga().Handle(new GetRetranslationCostEstimateQuery(), CancellationToken.None);

        Assert.Equal(200, estimate.TranslatedCount);
        Assert.Equal(200 * 0.016m, estimate.EstimatedUsd);
    }

    [Fact]
    public async Task TheStatusReportsAParkedClientBeforeAnythingElse()
    {
        _client.SetupGet(c => c.IsConfigured).Returns(false);

        var status = await Saga().Handle(new GetTranslationPipelineStatusQuery(), CancellationToken.None);

        Assert.False(status.ClientConfigured);
        Assert.Equal(30m, status.CeilingUsd);
        Assert.Equal(50, status.NightlyCount);
    }
}
