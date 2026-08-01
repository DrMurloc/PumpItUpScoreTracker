using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Everything a maker does to their own tool, and everything an admin does to it.
///     <para>
///         Grouped as one class because every operation needs the same three collaborators and the
///         same ownership check — the "Saga" shape this codebase uses for a feature's handlers.
///     </para>
/// </summary>
internal sealed class ToolManagementSaga :
    IRequestHandler<CreateToolCommand, Guid>,
    IRequestHandler<UpdateToolCommand>,
    IRequestHandler<SetToolAllToolsShareCommand>,
    IRequestHandler<SetToolWebhookCommand>,
    IRequestHandler<RequestToolListingCommand>,
    IRequestHandler<ApproveToolCommand>,
    IRequestHandler<RejectToolCommand>,
    IRequestHandler<DeleteToolCommand>,
    IRequestHandler<GetMyToolsQuery, IReadOnlyList<ToolRecord>>,
    IRequestHandler<GetToolQuery, ToolRecord?>,
    IRequestHandler<GetToolsAwaitingReviewQuery, IReadOnlyList<ToolRecord>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IToolRepository _tools;

    public ToolManagementSaga(IToolRepository tools, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTime)
    {
        _tools = tools;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Guid> Handle(CreateToolCommand request, CancellationToken cancellationToken)
    {
        var tool = Tool.Create(Guid.NewGuid(), _currentUser.User.Id, Name.From(request.Name), _dateTime.Now);
        await _tools.Save(tool, cancellationToken);

        // The maker is player one. Without this they cannot test their own tool against a real
        // account, and finding themselves in their own directory would be a silly first step.
        await _tools.GrantShare(tool.Id, _currentUser.User.Id, ShareSource.Direct, _dateTime.Now,
            cancellationToken);
        return tool.Id;
    }

    public async Task Handle(UpdateToolCommand request, CancellationToken cancellationToken)
    {
        var tool = await Owned(request.ToolId, cancellationToken);
        tool.Describe(Name.From(request.Name), request.Description,
            string.IsNullOrWhiteSpace(request.Url) ? null : new Uri(request.Url));
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(SetToolAllToolsShareCommand request, CancellationToken cancellationToken)
    {
        var tool = await Owned(request.ToolId, cancellationToken);
        tool.SetAcceptsAllToolsShare(request.Accepts);
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(SetToolWebhookCommand request, CancellationToken cancellationToken)
    {
        var tool = await Owned(request.ToolId, cancellationToken);
        var connected = await _tools.CountConnectedPlayers(request.ToolId, cancellationToken);
        tool.SetWebhook(request.Mode,
            string.IsNullOrWhiteSpace(request.Url) ? null : new Uri(request.Url), connected);
        tool.SetMixes(request.Mixes);
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(RequestToolListingCommand request, CancellationToken cancellationToken)
    {
        var tool = await Owned(request.ToolId, cancellationToken);
        tool.RequestListing();
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(ApproveToolCommand request, CancellationToken cancellationToken)
    {
        var tool = await AdminOnly(request.ToolId, cancellationToken);
        tool.Approve(_dateTime.Now);
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(RejectToolCommand request, CancellationToken cancellationToken)
    {
        var tool = await AdminOnly(request.ToolId, cancellationToken);
        tool.Reject(request.Reason);
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(DeleteToolCommand request, CancellationToken cancellationToken)
    {
        await Owned(request.ToolId, cancellationToken);
        await _tools.DeleteTool(request.ToolId, cancellationToken);
    }

    public async Task<IReadOnlyList<ToolRecord>> Handle(GetMyToolsQuery request,
        CancellationToken cancellationToken)
    {
        var tools = await _tools.GetToolsOwnedBy(_currentUser.User.Id, cancellationToken);
        return await Task.WhenAll(tools.Select(t => Project(t, cancellationToken)));
    }

    public async Task<ToolRecord?> Handle(GetToolQuery request, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(request.ToolId, cancellationToken);
        if (tool is null || (tool.OwnerUserId != _currentUser.User.Id && !_currentUser.User.IsAdmin))
            return null;

        return await Project(tool, cancellationToken);
    }

    public async Task<IReadOnlyList<ToolRecord>> Handle(GetToolsAwaitingReviewQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.User.IsAdmin) return Array.Empty<ToolRecord>();

        var tools = await _tools.GetToolsByVisibility(ToolVisibility.PendingApproval, cancellationToken);
        return await Task.WhenAll(tools.Select(t => Project(t, cancellationToken)));
    }

    private async Task<ToolRecord> Project(Tool tool, CancellationToken cancellationToken)
    {
        return new ToolRecord(tool.Id, tool.OwnerUserId, tool.Name.ToString(), tool.Description,
            tool.Url?.ToString(), tool.Visibility, tool.AcceptsAllToolsShare, tool.WebhookMode,
            tool.WebhookUrl?.ToString(), tool.Mixes.ToArray(),
            await _tools.CountConnectedPlayers(tool.Id, cancellationToken),
            tool.CreatedAt, tool.ApprovedAt, tool.RejectionReason);
    }

    private async Task<Tool> Owned(Guid toolId, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(toolId, cancellationToken)
                   ?? throw new ToolNotFoundException();
        if (tool.OwnerUserId != _currentUser.User.Id) throw new ToolNotFoundException();

        return tool;
    }

    private async Task<Tool> AdminOnly(Guid toolId, CancellationToken cancellationToken)
    {
        if (!_currentUser.User.IsAdmin) throw new ToolNotFoundException();

        return await _tools.GetTool(toolId, cancellationToken) ?? throw new ToolNotFoundException();
    }
}
