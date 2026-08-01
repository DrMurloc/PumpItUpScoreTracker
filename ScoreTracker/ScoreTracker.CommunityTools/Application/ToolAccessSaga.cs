using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Who may read whose scores: a player's connections, their all-tools preference, blocks, and
///     the resolution the API's authorization filter asks.
/// </summary>
internal sealed class ToolAccessSaga :
    IRequestHandler<ConnectToolCommand>,
    IRequestHandler<DisconnectToolCommand>,
    IRequestHandler<BlockToolCommand>,
    IRequestHandler<SetShareWithAllToolsCommand>,
    IRequestHandler<GetMyToolConnectionsQuery, IReadOnlyList<PlayerToolConnectionRecord>>,
    IRequestHandler<GetShareWithAllToolsQuery, bool>,
    IRequestHandler<GetPublicToolsQuery, IReadOnlyList<PublicToolRecord>>,
    IRequestHandler<GetToolReadablePlayersQuery, IReadOnlyList<Guid>>,
    IRequestHandler<CanToolReadPlayerQuery, bool>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IToolRepository _tools;
    private readonly IUserReader _users;

    public ToolAccessSaga(IToolRepository tools, IUserReader users, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTime)
    {
        _tools = tools;
        _users = users;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task Handle(ConnectToolCommand request, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(request.ToolId, cancellationToken)
                   ?? throw new ToolNotFoundException();

        await _tools.GrantShare(tool.Id, _currentUser.User.Id, ShareSource.Direct, _dateTime.Now,
            cancellationToken);
    }

    public async Task Handle(DisconnectToolCommand request, CancellationToken cancellationToken)
    {
        await _tools.RevokeShare(request.ToolId, _currentUser.User.Id, _dateTime.Now, cancellationToken);
    }

    public async Task Handle(BlockToolCommand request, CancellationToken cancellationToken)
    {
        // Blocking also drops a direct grant: a player saying "not this one" means it, whichever
        // route the tool arrived by.
        await _tools.RevokeShare(request.ToolId, _currentUser.User.Id, _dateTime.Now, cancellationToken);
        await _tools.BlockTool(request.ToolId, _currentUser.User.Id, _dateTime.Now, cancellationToken);
    }

    public async Task Handle(SetShareWithAllToolsCommand request, CancellationToken cancellationToken)
    {
        await _tools.SetShareWithAllTools(_currentUser.User.Id, request.Share, _dateTime.Now,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerToolConnectionRecord>> Handle(GetMyToolConnectionsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var toolIds = await _tools.GetToolIdsReading(userId, cancellationToken);
        var direct = (await _tools.GetSharesForUser(userId, cancellationToken))
            .ToDictionary(s => s.ToolId);

        var result = new List<PlayerToolConnectionRecord>();
        foreach (var toolId in toolIds)
        {
            var tool = await _tools.GetTool(toolId, cancellationToken);
            if (tool is null) continue;

            var share = direct.TryGetValue(toolId, out var found) ? found : null;
            result.Add(new PlayerToolConnectionRecord(tool.Id, tool.Name.ToString(), tool.Description,
                await OwnerName(tool.OwnerUserId, cancellationToken),
                share?.Source ?? ShareSource.AllTools,
                tool.RequiresExplicitShare,
                share?.GrantedAt ?? tool.CreatedAt));
        }

        return result.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<bool> Handle(GetShareWithAllToolsQuery request, CancellationToken cancellationToken)
    {
        return await _tools.GetShareWithAllTools(_currentUser.User.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<PublicToolRecord>> Handle(GetPublicToolsQuery request,
        CancellationToken cancellationToken)
    {
        var tools = await _tools.GetToolsByVisibility(ToolVisibility.Public, cancellationToken);
        var result = new List<PublicToolRecord>();
        foreach (var tool in tools)
            result.Add(new PublicToolRecord(tool.Id, tool.Name.ToString(), tool.Description,
                tool.Url?.ToString(), await OwnerName(tool.OwnerUserId, cancellationToken),
                tool.RequiresExplicitShare,
                await _tools.CountConnectedPlayers(tool.Id, cancellationToken), tool.ApprovedAt));

        return result.OrderByDescending(r => r.ConnectedPlayers).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> Handle(GetToolReadablePlayersQuery request,
        CancellationToken cancellationToken)
    {
        return await _tools.GetReadablePlayerIds(request.ToolId, cancellationToken);
    }

    public async Task<bool> Handle(CanToolReadPlayerQuery request, CancellationToken cancellationToken)
    {
        return await _tools.CanRead(request.ToolId, request.UserId, cancellationToken);
    }

    private async Task<string> OwnerName(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var owner = await _users.GetUser(ownerUserId, cancellationToken);
        return owner?.Name.ToString() ?? string.Empty;
    }
}
