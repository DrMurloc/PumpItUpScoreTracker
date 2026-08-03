using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Barring a maker from making tools, and letting them back.
///     <para>
///         Rule 2 threatens this and nothing in the software could do it: deleting a tool never
///         stopped its maker registering another thirty seconds later.
///     </para>
///     <para>
///         Admin-only, and deliberately without a bus event. A ban has no downstream consumer — its
///         entire effect is computed from the row at read time — and publishing a fact nobody
///         subscribes to would invite someone to react to it later by deleting something.
///     </para>
/// </summary>
internal sealed class ToolMakerBanSaga :
    IRequestHandler<BanToolMakerCommand>,
    IRequestHandler<LiftToolMakerBanCommand>,
    IRequestHandler<SetToolMakerBanNotesCommand>,
    IRequestHandler<GetToolMakerBansQuery, IReadOnlyList<ToolMakerBanRecord>>,
    IRequestHandler<IsToolMakerBannedQuery, bool>
{
    private readonly IToolMakerBanRepository _bans;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IUserReader _users;

    public ToolMakerBanSaga(IToolMakerBanRepository bans, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTime, IUserReader users)
    {
        _bans = bans;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _users = users;
    }

    public async Task Handle(BanToolMakerCommand request, CancellationToken cancellationToken)
    {
        RequireAdmin();

        // Banning yourself would lock the only account that can lift it.
        if (request.UserId == _currentUser.User.Id)
            throw new ToolListingException("You can't ban yourself from making tools.");

        await _bans.Ban(new ToolMakerBan(request.UserId, _dateTime.Now, _currentUser.User.Id,
            Blank(request.Notes)), cancellationToken);
    }

    public async Task Handle(LiftToolMakerBanCommand request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await _bans.Lift(request.UserId, cancellationToken);
    }

    public async Task Handle(SetToolMakerBanNotesCommand request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await _bans.SetNotes(request.UserId, Blank(request.Notes), cancellationToken);
    }

    public async Task<IReadOnlyList<ToolMakerBanRecord>> Handle(GetToolMakerBansQuery request,
        CancellationToken cancellationToken)
    {
        RequireAdmin();

        var bans = await _bans.GetBans(cancellationToken);
        var records = new List<ToolMakerBanRecord>();
        foreach (var ban in bans)
        {
            var user = await _users.GetUser(ban.UserId, cancellationToken);
            records.Add(new ToolMakerBanRecord(ban.UserId, user?.Name.ToString() ?? string.Empty,
                ban.BannedAt, ban.Notes));
        }

        return records;
    }

    /// <summary>
    ///     Not admin-gated: the create path asks this about the caller, and a maker being told why
    ///     they cannot register is the whole point of it being answerable.
    /// </summary>
    public async Task<bool> Handle(IsToolMakerBannedQuery request, CancellationToken cancellationToken)
    {
        return await _bans.GetBan(request.UserId, cancellationToken) is not null;
    }

    private void RequireAdmin()
    {
        if (!_currentUser.IsLoggedIn || !_currentUser.User.IsAdmin) throw new ToolNotFoundException();
    }

    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
