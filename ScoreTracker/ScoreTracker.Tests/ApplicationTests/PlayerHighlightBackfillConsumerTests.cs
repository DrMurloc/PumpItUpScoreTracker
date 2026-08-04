using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Communities.Domain;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerHighlightBackfillConsumerTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    // Exactly what the writer produced: enums as strings.
    private const string PgPayload =
        """[{"Kind":"NotablePg","ChartName":"Bee","RarityShare":0.004}]""";

    private readonly Mock<ICommunityHighlightRepository> _index = new();
    private readonly Mock<IMediator> _mediator = new();

    private PlayerHighlightBackfillConsumer Consumer() =>
        new(_index.Object, _mediator.Object, NullLogger<PlayerHighlightBackfillConsumer>.Instance);

    private void LegacyRowsAre(params LegacyHighlightPayload[] rows) =>
        _index.Setup(i => i.GetLegacyPayloads(It.IsAny<CancellationToken>())).ReturnsAsync(rows);

    private static LegacyHighlightPayload Row(string payload = PgPayload,
        int schemaVersion = PlayerHighlightSchema.CurrentVersion) =>
        new(Guid.NewGuid(), Guid.NewGuid(), MixEnum.Phoenix, When, null, payload, schemaVersion);

    private static ConsumeContext<BackfillPlayerHighlightsCommand> Context()
    {
        var ctx = new Mock<ConsumeContext<BackfillPlayerHighlightsCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new BackfillPlayerHighlightsCommand());
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task CopiesEachLegacyPayloadIntoTheLedger()
    {
        var row = Row();
        LegacyRowsAre(row);

        await Consumer().Consume(Context());

        _mediator.Verify(m => m.Send(It.Is<StorePlayerHighlightCommand>(c =>
            c.EventId == row.EventId && c.UserId == row.UserId && c.Mix == MixEnum.Phoenix
            && c.Wins.Count == 1 && c.Wins[0].Kind == WinKind.NotablePg
            && c.Wins[0].ChartName == "Bee"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A stale-schema payload describes a moment summarised before the vocabulary was
    ///     complete. The reader refuses to render it, so copying it forward buys nothing.
    /// </summary>
    [Fact]
    public async Task SkipsPayloadsStampedWithAnOlderSchema()
    {
        LegacyRowsAre(Row(schemaVersion: PlayerHighlightSchema.CurrentVersion - 1));

        await Consumer().Consume(Context());

        _mediator.Verify(m => m.Send(It.IsAny<StorePlayerHighlightCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     A one-shot admin button gets pressed twice. An unreadable row must not turn the second
    ///     press into a failed run that copies less than the first.
    /// </summary>
    [Fact]
    public async Task SurvivesAnUnreadablePayloadAndKeepsGoing()
    {
        var good = Row();
        LegacyRowsAre(Row("{ not json at all"), good);

        var thrown = await Record.ExceptionAsync(() => Consumer().Consume(Context()));

        Assert.Null(thrown);
        _mediator.Verify(m => m.Send(It.Is<StorePlayerHighlightCommand>(c => c.EventId == good.EventId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendsNothingForAnEmptyWinList()
    {
        LegacyRowsAre(Row("[]"));

        await Consumer().Consume(Context());

        _mediator.Verify(m => m.Send(It.IsAny<StorePlayerHighlightCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
