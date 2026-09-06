using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>API keys and invite links: minting, revoking, and resolving one back to its tool.</summary>
internal sealed class ToolKeySaga :
    IRequestHandler<CreateToolApiKeyCommand, MintedApiKey>,
    IRequestHandler<RevokeToolApiKeyCommand>,
    IRequestHandler<CreateToolInviteLinkCommand, Guid>,
    IRequestHandler<RevokeToolInviteLinkCommand>,
    IRequestHandler<SetToolInviteLinkNoteCommand>,
    IRequestHandler<GetToolApiKeysQuery, IReadOnlyList<ApiKeyRecord>>,
    IRequestHandler<GetToolInviteLinksQuery, IReadOnlyList<ToolInviteLinkRecord>>,
    IRequestHandler<GetToolInvitePreviewQuery, ToolInvitePreview?>,
    IRequestHandler<GetToolByApiKeyQuery, ToolKeyPrincipal?>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IToolKeyRepository _keys;
    private readonly IToolRepository _tools;
    private readonly IUserReader _users;

    public ToolKeySaga(IToolKeyRepository keys, IToolRepository tools, IUserReader users,
        ICurrentUserAccessor currentUser, IDateTimeOffsetAccessor dateTime)
    {
        _keys = keys;
        _tools = tools;
        _users = users;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<MintedApiKey> Handle(CreateToolApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        await AssertManageable(request.ToolId, cancellationToken);

        var live = (await _keys.GetKeys(request.ToolId, cancellationToken))
            .Count(k => k.RevokedAt is null && (k.ExpiresAt is null || k.ExpiresAt > _dateTime.Now));
        if (live >= ApiKeyMint.MaxActiveKeys)
            throw new ToolListingException(
                $"A tool can have {ApiKeyMint.MaxActiveKeys} keys live at once — enough to roll one " +
                "without downtime. Revoke the one you are replacing first.");

        var (key, hash, last4) = ApiKeyMint.Mint();
        var id = Guid.NewGuid();
        await _keys.AddKey(request.ToolId, id, request.Name, hash, last4, _dateTime.Now,
            request.ExpiresAt, cancellationToken);

        // The only time the plaintext exists outside the caller's hands. Nothing stores it.
        return new MintedApiKey(id, key, request.ExpiresAt);
    }

    public async Task Handle(RevokeToolApiKeyCommand request, CancellationToken cancellationToken)
    {
        await AssertManageable(request.ToolId, cancellationToken);
        await _keys.RevokeKey(request.ToolId, request.KeyId, _dateTime.Now, cancellationToken);
    }

    public async Task<Guid> Handle(CreateToolInviteLinkCommand request, CancellationToken cancellationToken)
    {
        await AssertManageable(request.ToolId, cancellationToken);
        var code = Guid.NewGuid();
        await _keys.AddInviteCode(request.ToolId, code, _dateTime.Now, cancellationToken);
        return code;
    }

    public async Task Handle(RevokeToolInviteLinkCommand request, CancellationToken cancellationToken)
    {
        await AssertManageable(request.ToolId, cancellationToken);
        await _keys.RevokeInviteCode(request.ToolId, request.Code, _dateTime.Now, cancellationToken);
    }

    public async Task Handle(SetToolInviteLinkNoteCommand request, CancellationToken cancellationToken)
    {
        await AssertManageable(request.ToolId, cancellationToken);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _keys.SetInviteCodeNote(request.ToolId, request.Code, note, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKeyRecord>> Handle(GetToolApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        await AssertManageable(request.ToolId, cancellationToken);
        return (await _keys.GetKeys(request.ToolId, cancellationToken))
            .Select(k => new ApiKeyRecord(k.Id, k.Name, k.Last4, k.CreatedAt, k.ExpiresAt,
                k.LastUsedAt, k.RevokedAt is not null))
            .ToArray();
    }

    public async Task<IReadOnlyList<ToolInviteLinkRecord>> Handle(GetToolInviteLinksQuery request,
        CancellationToken cancellationToken)
    {
        await AssertManageable(request.ToolId, cancellationToken);
        return (await _keys.GetInviteCodes(request.ToolId, cancellationToken))
            .Select(i => new ToolInviteLinkRecord(i.Code, i.Note, i.CreatedAt))
            .ToArray();
    }

    /// <summary>
    ///     What an invited player is shown before deciding. Reachable without owning the tool, and
    ///     without being signed in — the landing page is the one logged-out surface in the feature.
    /// </summary>
    public async Task<ToolInvitePreview?> Handle(GetToolInvitePreviewQuery request,
        CancellationToken cancellationToken)
    {
        var toolId = await _keys.ResolveToolByInviteCode(request.Code, cancellationToken);
        if (toolId is null) return null;

        var tool = await _tools.GetTool(toolId.Value, cancellationToken);
        if (tool is null) return null;

        var owner = await _users.GetUser(tool.OwnerUserId, cancellationToken);
        return new ToolInvitePreview(tool.Id, tool.Name.ToString(), tool.Description,
            tool.Url?.ToString(), owner?.Name.ToString() ?? string.Empty,
            tool.Visibility == ToolVisibility.Public, tool.RequiresExplicitShare,
            await _tools.CountConnectedPlayers(tool.Id, cancellationToken),
            tool.RepositoryUrl?.ToString(), tool.Kind);
    }

    public async Task<ToolKeyPrincipal?> Handle(GetToolByApiKeyQuery request,
        CancellationToken cancellationToken)
    {
        // Shape-check before touching the database so a malformed bearer token never becomes a query.
        if (!ApiKeyMint.LooksLikeAKey(request.Key)) return null;

        var resolution = await _keys.ResolveToolByKeyHash(ApiKeyMint.HashOf(request.Key), _dateTime.Now,
            cancellationToken);
        // An expired key is named for the console's sake and still fails here.
        return resolution is { IsExpired: false }
            ? new ToolKeyPrincipal(resolution.ToolId, resolution.KeyName)
            : null;
    }

    /// <summary>
    ///     Keys and invite links belong to the tool's owner, and to an admin — see
    ///     <c>ToolManagementSaga.Manageable</c> for what that grant covers and why.
    /// </summary>
    private async Task AssertManageable(Guid toolId, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(toolId, cancellationToken);
        if (tool is null || (tool.OwnerUserId != _currentUser.User.Id && !_currentUser.User.IsAdmin))
            throw new ToolNotFoundException();
    }
}
