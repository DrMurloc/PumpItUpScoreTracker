using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.ChartComments.Application;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Communities.Contracts.Events;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommunityDeletionConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheDeletedClubsCommentsAreArchivedUnderItsLastKnownName()
    {
        var communityId = Guid.NewGuid();
        var archivedComment = Guid.NewGuid();
        var archive = new Mock<ICommentArchiveRepository>();
        archive.Setup(a => a.ArchiveCommunity(It.IsAny<Guid>(), It.IsAny<Name>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { archivedComment });
        var context = new Mock<ConsumeContext<CommunityDeletedEvent>>();
        context.SetupGet(c => c.Message)
            .Returns(new CommunityDeletedEvent(communityId, Name.From("Murloc Lab")));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await new CommunityDeletionConsumer(archive.Object, FakeDateTime.At(Now).Object)
            .Consume(context.Object);

        archive.Verify(a => a.ArchiveCommunity(communityId,
            It.Is<Name>(name => (string)name == "Murloc Lab"), Now, CancellationToken.None), Times.Once);
        // The pipeline drops what it held for the archived comments — a queued text for a dead
        // club is money waiting to be wasted.
        context.Verify(c => c.Publish(It.Is<ScoreTracker.Translations.Contracts.Messages
                .DiscardTranslationRequestsCommand>(discard =>
                discard.SourceKeys.Count == 1 && discard.SourceKeys[0].Contains(archivedComment.ToString("N"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AClubWithNoCommentsPublishesNoDiscard()
    {
        var archive = new Mock<ICommentArchiveRepository>();
        archive.Setup(a => a.ArchiveCommunity(It.IsAny<Guid>(), It.IsAny<Name>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        var context = new Mock<ConsumeContext<CommunityDeletedEvent>>();
        context.SetupGet(c => c.Message)
            .Returns(new CommunityDeletedEvent(Guid.NewGuid(), Name.From("Murloc Lab")));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await new CommunityDeletionConsumer(archive.Object, FakeDateTime.At(Now).Object)
            .Consume(context.Object);

        context.Verify(c => c.Publish(
            It.IsAny<ScoreTracker.Translations.Contracts.Messages.DiscardTranslationRequestsCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
