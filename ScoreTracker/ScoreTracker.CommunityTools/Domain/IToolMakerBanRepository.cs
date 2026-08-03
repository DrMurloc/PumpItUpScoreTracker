namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     A maker barred from making tools, and the record of it.
///     <para>
///         A ban <b>disables, it never deletes</b>. <c>DeleteTool</c> hard-deletes across eight
///         tables including the activity log and every delivery record — the exact evidence a
///         disputed ban needs. So a ban is a row here, every effect of it is computed from that row,
///         and lifting it restores everything untouched. Delete remains its own separate action for
///         when the owner actually wants it gone.
///     </para>
/// </summary>
internal interface IToolMakerBanRepository
{
    Task<ToolMakerBan?> GetBan(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolMakerBan>> GetBans(CancellationToken cancellationToken = default);

    Task Ban(ToolMakerBan ban, CancellationToken cancellationToken = default);

    Task Lift(Guid userId, CancellationToken cancellationToken = default);

    Task SetNotes(Guid userId, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Which of these users are banned, for resolving a page of tools in one round trip.</summary>
    Task<IReadOnlySet<Guid>> BannedAmong(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     One ban. <see cref="Notes" /> is the owner's own scratch space — freeform, editable
///     afterwards, and seen by nobody else.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolMakerBan(Guid UserId, DateTimeOffset BannedAt, Guid BannedByUserId,
    string? Notes);
