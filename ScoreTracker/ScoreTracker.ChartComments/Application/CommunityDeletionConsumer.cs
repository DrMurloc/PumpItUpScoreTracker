using MassTransit;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Communities.Contracts.Events;
using ScoreTracker.Translations.Contracts.Messages;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     Settles what this vertical holds against a deleted club: comments move to the archive,
///     and the votes, revisions, reports and mutes that only meant something while the club
///     lived go with it. Idempotent — the transport re-fires, and a second pass finds nothing
///     left to move.
/// </summary>
internal sealed class CommunityDeletionConsumer : IConsumer<CommunityDeletedEvent>
{
    private readonly ICommentArchiveRepository _archive;
    private readonly IDateTimeOffsetAccessor _clock;

    public CommunityDeletionConsumer(ICommentArchiveRepository archive, IDateTimeOffsetAccessor clock)
    {
        _archive = archive;
        _clock = clock;
    }

    public async Task Consume(ConsumeContext<CommunityDeletedEvent> context)
    {
        var archived = await _archive.ArchiveCommunity(context.Message.CommunityId,
            context.Message.CommunityName, _clock.Now, context.CancellationToken);

        // Whatever the pipeline still holds for the archived comments answers no question
        // anybody can still ask — and must not be spent on.
        if (archived.Count > 0)
            await context.Publish(new DiscardTranslationRequestsCommand(
                archived.Select(CommentSourceKeys.For).ToArray()), context.CancellationToken);
    }
}
