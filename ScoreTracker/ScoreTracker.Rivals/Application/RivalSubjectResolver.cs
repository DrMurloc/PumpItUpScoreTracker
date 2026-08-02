using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     Turns stored edges into <see cref="RivalSubject" />s — the one place the tag/user duality
///     lives (docs/design/rivals.md §2.1). Every rival surface consumes subjects; nothing else
///     should ever have to ask "is this one a ghost".
///     <para>
///         Both sides resolve in ONE round trip each, not one per rival: a roster with three
///         hundred edges on it is the shape this will actually be exercised in.
///     </para>
/// </summary>
internal sealed class RivalSubjectResolver
{
    private readonly IMediator _mediator;
    private readonly IUserReader _users;

    public RivalSubjectResolver(IUserReader users, IMediator mediator)
    {
        _users = users;
        _mediator = mediator;
    }

    public async Task<IReadOnlyList<RivalSubject>> Resolve(IReadOnlyList<RivalEdge> edges, MixEnum mix,
        CancellationToken cancellationToken)
    {
        if (edges.Count == 0) return Array.Empty<RivalSubject>();

        var userIds = edges.Where(e => e.TargetUserId != null).Select(e => e.TargetUserId!.Value).Distinct()
            .ToArray();
        var tags = edges.Where(e => e.TargetTag != null).Select(e => e.TargetTag!).Distinct().ToArray();

        var users = userIds.Length == 0
            ? new Dictionary<Guid, ScoreTracker.Domain.Models.User>()
            : (await _users.GetUsers(userIds, cancellationToken)).ToDictionary(u => u.Id);
        var ghosts = tags.Length == 0
            ? new Dictionary<string, OfficialPlayerResolution>(StringComparer.OrdinalIgnoreCase)
            : (await _mediator.Send(new ResolveOfficialPlayersQuery(mix, tags), cancellationToken))
            .ToDictionary(r => r.Tag, StringComparer.OrdinalIgnoreCase);

        var subjects = new List<RivalSubject>(edges.Count);
        foreach (var edge in edges)
        {
            var subject = edge.TargetUserId is { } userId
                ? SiteSubject(edge, userId, users)
                : GhostSubject(edge, ghosts);
            if (subject != null) subjects.Add(subject);
        }

        return subjects;
    }

    /// <summary>
    ///     A site rival answers for everything except official standings, which need a linked board
    ///     tag they may not have. A user the reader cannot resolve has been deleted between the add
    ///     and this read — the row drops, because there is nobody left to name.
    /// </summary>
    private static RivalSubject? SiteSubject(RivalEdge edge, Guid userId,
        IReadOnlyDictionary<Guid, ScoreTracker.Domain.Models.User> users)
    {
        if (!users.TryGetValue(userId, out var user)) return null;
        return new RivalSubject(edge.Id, userId, null, user.Name.ToString(), user.ProfileImage,
            IsOnCurrentBoards: false,
            RivalCapabilities.LiveScores | RivalCapabilities.FolderCompare | RivalCapabilities.Progression,
            edge.AddedAt);
    }

    /// <summary>
    ///     A board-only rival answers for standings and nothing else. An unresolvable tag still
    ///     renders — it is somebody the user deliberately chose, and a row saying the tag left the
    ///     boards is something they can act on, where a silently vanished row is not.
    /// </summary>
    private static RivalSubject GhostSubject(RivalEdge edge,
        IReadOnlyDictionary<string, OfficialPlayerResolution> ghosts)
    {
        var tag = edge.TargetTag!;
        if (!ghosts.TryGetValue(tag, out var ghost))
            return new RivalSubject(edge.Id, null, tag, tag, null, IsOnCurrentBoards: false,
                RivalCapabilities.None, edge.AddedAt);

        // A tag that linked between the add and now: the promote consumer will rewrite the row,
        // but this read must not wait for it.
        var capabilities = ghost.LinkedUserId == null
            ? RivalCapabilities.OfficialStandings
            : RivalCapabilities.LiveScores | RivalCapabilities.FolderCompare |
              RivalCapabilities.Progression | RivalCapabilities.OfficialStandings;

        return new RivalSubject(edge.Id, ghost.LinkedUserId, ghost.Tag, ghost.Tag, ghost.Avatar,
            ghost.IsOnCurrentBoards, capabilities, edge.AddedAt);
    }
}
