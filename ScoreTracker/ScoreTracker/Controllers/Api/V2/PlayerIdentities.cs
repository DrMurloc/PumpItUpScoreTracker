using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>The account and game tag every cross-player API row is built from.</summary>
internal sealed record PlayerIdentity(User User, string? GameTag)
{
    public Guid UserId => User.Id;

    public string Username => User.Name.ToString();
}

/// <summary>
///     Resolves the accounts and game tags for a page of user ids in three round trips, however
///     long the page is. The per-player reads do this one player at a time, which is fine for one
///     profile and is a hundred round trips behind a hundred-row page.
///     <para>
///         The tag rule is the one the single profile read applies: one tag per account, the newer
///         Phoenix mix's link first, because the tag is an AM Pass account setting shared across
///         the Phoenix mixes and the newer mix's row is the more recently confirmed snapshot of it.
///         A blank tag counts as no link, so that account falls through to the older mix.
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

        var tags = await LinkedTags(mediator, MixEnum.Phoenix2, ids);
        var unlinked = ids.Where(id => !tags.ContainsKey(id)).ToArray();
        if (unlinked.Length > 0)
            foreach (var (id, tag) in await LinkedTags(mediator, MixEnum.Phoenix, unlinked))
                tags[id] = tag;

        return users.ToDictionary(u => u.Id,
            u => new PlayerIdentity(u, tags.TryGetValue(u.Id, out var tag) ? tag : null));
    }

    private static async Task<Dictionary<Guid, string>> LinkedTags(IMediator mediator, MixEnum mix,
        IReadOnlyCollection<Guid> userIds)
    {
        return (await mediator.Send(new GetLinkedOfficialPlayerTagsQuery(mix, userIds)))
            .Where(link => !string.IsNullOrWhiteSpace(link.Value))
            .ToDictionary(link => link.Key, link => link.Value);
    }
}
