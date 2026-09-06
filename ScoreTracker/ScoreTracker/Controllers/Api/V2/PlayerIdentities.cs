using MediatR;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>The three identity fields every cross-player API row carries.</summary>
internal sealed record PlayerIdentity(Guid UserId, string Username, string? GameTag);

/// <summary>
///     Resolves usernames and game tags for a page of user ids in three round trips, however
///     long the page is. The per-player reads do this one player at a time, which is fine for one
///     profile and is a hundred round trips behind a hundred-row page.
///     <para>
///         The tag rule is the one <c>/api/v2/players</c> applies: one tag per account, the newer
///         Phoenix mix's link first, because the tag is an AM Pass account setting shared across
///         the Phoenix mixes and the newer mix's row is the more recently confirmed snapshot of it.
///     </para>
/// </summary>
internal static class PlayerIdentities
{
    public static async Task<IReadOnlyDictionary<Guid, PlayerIdentity>> Resolve(IMediator mediator,
        IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, PlayerIdentity>();

        var users = await mediator.Send(new GetUsersByIdsQuery(userIds));
        var ids = users.Select(u => u.Id).ToArray();

        var tags = new Dictionary<Guid, string>(
            await mediator.Send(new GetLinkedOfficialPlayerTagsQuery(MixEnum.Phoenix2, ids)));
        var unlinked = ids.Where(id => !tags.ContainsKey(id)).ToArray();
        if (unlinked.Length > 0)
            foreach (var (id, tag) in await mediator.Send(new GetLinkedOfficialPlayerTagsQuery(MixEnum.Phoenix, unlinked)))
                tags[id] = tag;

        return users.ToDictionary(u => u.Id,
            u => new PlayerIdentity(u.Id, u.Name.ToString(), tags.TryGetValue(u.Id, out var tag) ? tag : null));
    }
}
