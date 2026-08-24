using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.ChartComments.Application;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Tests.TestHelpers;
using ScoreTracker.Translations.Contracts.Events;
using Xunit;
// The 2000-char rendering limit and the failed-event stamp clearing are both exercised below.

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommentTranslationSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 30, 0, TimeSpan.Zero);

    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<ICommentRenderingRepository> _renderings = new();

    private CommentTranslationSaga Saga()
    {
        return new CommentTranslationSaga(_comments.Object, _renderings.Object,
            FakeDateTime.At(Now).Object, NullLogger<CommentTranslationSaga>.Instance);
    }

    private static ConsumeContext<TextTranslatedEvent> ContextFor(TextTranslatedEvent message)
    {
        var context = new Mock<ConsumeContext<TextTranslatedEvent>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        return context.Object;
    }

    private Comment StoredComment(string text, CommentAudience? audience = null)
    {
        var comment = Comment.Post(Guid.NewGuid(), Guid.NewGuid(), audience ?? CommentAudience.Public,
            text, Now.AddDays(-1));
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        return comment;
    }

    [Fact]
    public async Task RenderingsLandWithTheirLinksSubstitutedBack()
    {
        var comment = StoredComment("proof: https://youtu.be/abc");
        var marked = CommentText.ExtractLinks(comment.Text);

        await Saga().Consume(ContextFor(new TextTranslatedEvent(CommentSourceKeys.For(comment.Id),
            marked.Text, "en",
            new Dictionary<string, string> { ["es-ES"] = "la prueba: ⟦1⟧" }, "sonnet+sonnet")));

        _renderings.Verify(r => r.StoreTranslation(comment.Id, "en",
            It.Is<IReadOnlyDictionary<string, string>>(kept =>
                kept["es-ES"] == "la prueba: https://youtu.be/abc"),
            "sonnet+sonnet", Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARenderingWhoseLinksDifferIsNeverStored()
    {
        var comment = StoredComment("proof: https://youtu.be/abc");
        var marked = CommentText.ExtractLinks(comment.Text);

        await Saga().Consume(ContextFor(new TextTranslatedEvent(CommentSourceKeys.For(comment.Id),
            marked.Text, "en",
            new Dictionary<string, string>
            {
                ["es-ES"] = "la prueba: ⟦1⟧ y https://phish.example",
                ["fr-FR"] = "la preuve : ⟦1⟧"
            }, "sonnet+sonnet")));

        _renderings.Verify(r => r.StoreTranslation(comment.Id, "en",
            It.Is<IReadOnlyDictionary<string, string>>(kept =>
                kept.Count == 1 && kept.ContainsKey("fr-FR")),
            It.IsAny<string>(), Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnEventForAnEditedCommentIsStaleAndIgnored()
    {
        var comment = StoredComment("the new words");

        await Saga().Consume(ContextFor(new TextTranslatedEvent(CommentSourceKeys.For(comment.Id),
            "the old words", "en",
            new Dictionary<string, string> { ["es-ES"] = "las palabras viejas" }, "sonnet+sonnet")));

        _renderings.Verify(r => r.StoreTranslation(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SomebodyElsesSourceKeysAreNotOursToActOn()
    {
        await Saga().Consume(ContextFor(new TextTranslatedEvent("community-blurb:abc", "hello", "en",
            new Dictionary<string, string> { ["es-ES"] = "hola" }, "sonnet+sonnet")));

        _comments.Verify(c => c.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFailureClearsTheStampSoTheBadgeStopsPromising()
    {
        var commentId = Guid.NewGuid();
        var context = new Mock<ConsumeContext<TextTranslationFailedEvent>>();
        context.SetupGet(c => c.Message)
            .Returns(new TextTranslationFailedEvent(CommentSourceKeys.For(commentId)));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await Saga().Consume(context.Object);

        _renderings.Verify(r => r.ClearTranslationQueued(commentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnOversizeRenderingIsDiscardedNeverTruncated()
    {
        // A cut rendering's link set was never the one verified; mid-URL is exactly where a cut
        // would land.
        var comment = StoredComment("short source");

        await Saga().Consume(ContextFor(new TextTranslatedEvent(CommentSourceKeys.For(comment.Id),
            "short source", "en",
            new Dictionary<string, string>
            {
                ["es-ES"] = new string('a', 2001),
                ["fr-FR"] = "court"
            }, "sonnet+sonnet")));

        _renderings.Verify(r => r.StoreTranslation(comment.Id, "en",
            It.Is<IReadOnlyDictionary<string, string>>(kept =>
                kept.Count == 1 && kept.ContainsKey("fr-FR")),
            It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ANoteNeverGainsRenderingsEvenIfOneWasSomehowQueued()
    {
        var note = StoredComment("just for me", CommentAudience.Private);

        await Saga().Consume(ContextFor(new TextTranslatedEvent(CommentSourceKeys.For(note.Id),
            "just for me", "en",
            new Dictionary<string, string> { ["es-ES"] = "solo para mí" }, "sonnet+sonnet")));

        _renderings.Verify(r => r.StoreTranslation(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
