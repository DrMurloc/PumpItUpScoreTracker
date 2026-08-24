using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     What a community's deletion does to the comments it leaves behind: the words move to the
///     archive, and everything that only meant something while the club lived — votes, revisions,
///     reports, mutes — goes. Vertical-internal, like every ChartComments port.
/// </summary>
internal interface ICommentArchiveRepository
{
    /// <summary>
    ///     Archives every comment of one deleted community and purges its votes, revisions,
    ///     reports (open and resolved — a report on an archived comment is a row nobody can
    ///     open), and the club's mutes. One transaction; idempotent, because the bus re-fires
    ///     and a second pass must find nothing left to move.
    /// </summary>
    /// <summary>Returns the archived comment ids, so the caller can clear the translation queue.</summary>
    Task<IReadOnlyList<Guid>> ArchiveCommunity(Guid communityId, Name communityName, DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
