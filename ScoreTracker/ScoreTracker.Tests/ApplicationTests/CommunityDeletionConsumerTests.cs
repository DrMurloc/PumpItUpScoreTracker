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
        var archive = new Mock<ICommentArchiveRepository>();
        var context = new Mock<ConsumeContext<CommunityDeletedEvent>>();
        context.SetupGet(c => c.Message)
            .Returns(new CommunityDeletedEvent(communityId, Name.From("Murloc Lab")));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await new CommunityDeletionConsumer(archive.Object, FakeDateTime.At(Now).Object)
            .Consume(context.Object);

        archive.Verify(a => a.ArchiveCommunity(communityId,
            It.Is<Name>(name => (string)name == "Murloc Lab"), Now, CancellationToken.None), Times.Once);
    }
}
