using MediatR;
using ScoreTracker.Communities.Contracts.Queries;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     Who shares a community with the caller — the second of the four add-time bases
///     (docs/design/rivals.md D9), and the same set the site-side picker offers.
///     <para>
///         USER-CREATED communities only. World and the country communities are joined
///         automatically, so counting them would make "people you know" mean everybody and the
///         private-account gate would protect nobody.
///     </para>
/// </summary>
internal sealed class RivalAudienceReader
{
    private readonly IMediator _mediator;

    public RivalAudienceReader(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IReadOnlySet<Guid>> GetClubmates(CancellationToken cancellationToken)
    {
        var communities = (await _mediator.Send(new GetMyCommunitiesQuery(), cancellationToken))
            .Where(c => !c.IsRegional && c.CommunityName != WorldCommunity)
            .Select(c => c.CommunityName)
            .ToArray();

        var members = new HashSet<Guid>();
        foreach (var community in communities)
            members.UnionWith(await _mediator.Send(new GetCommunityMembersQuery(community), cancellationToken));
        return members;
    }

    /// <summary>
    ///     Every account joins this on creation and, unlike the country communities, it is not
    ///     flagged regional — so "clubs you chose" has to name it to exclude it.
    /// </summary>
    private const string WorldCommunity = "World";
}
