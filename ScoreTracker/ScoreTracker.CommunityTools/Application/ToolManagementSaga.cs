using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using System.Net;
using Microsoft.Extensions.Options;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Wiring;
using ScoreTracker.CommunityTools.Infrastructure;
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
    IRequestHandler<SetToolOutboundHeaderCommand>,
    IRequestHandler<SetToolVerificationSecretCommand>,
    IRequestHandler<VerifyToolWebhookCommand, WebhookVerificationResult>,
    IRequestHandler<RequestToolListingCommand>,
    IRequestHandler<ApproveToolCommand>,
    IRequestHandler<RejectToolCommand>,
    IRequestHandler<DeleteToolCommand>,
    IRequestHandler<CheckToolRepositoryCommand, RepositoryCheckResult>,
    IRequestHandler<GetMyToolsQuery, IReadOnlyList<ToolRecord>>,
    IRequestHandler<GetAllToolsQuery, IReadOnlyList<ToolRecord>>,
    IRequestHandler<GetToolQuery, ToolRecord?>,
    IRequestHandler<GetToolsAwaitingReviewQuery, IReadOnlyList<ToolRecord>>
{
    private readonly IWebhookDeliveryClient _client;
    private readonly IOptions<CommunityToolsConfiguration> _configuration;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IMediator _mediator;
    private readonly IToolSecretReader _secrets;
    private readonly IToolMakerBanRepository _bans;
    private readonly IRepositoryReachabilityClient _repositories;
    private readonly IToolRepository _tools;
    private readonly IUserReader _users;

    public ToolManagementSaga(IToolRepository tools, IUserReader users, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTime, IMediator mediator, IToolSecretReader secrets,
        IWebhookDeliveryClient client, IOptions<CommunityToolsConfiguration> configuration,
        IRepositoryReachabilityClient repositories, IToolMakerBanRepository bans)
    {
        _bans = bans;
        _repositories = repositories;
        _configuration = configuration;
        _tools = tools;
        _users = users;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _mediator = mediator;
        _secrets = secrets;
        _client = client;
    }

    /// <summary>
    ///     Stores the header a maker's server checks. Blank value keeps the stored one, so editing
    ///     the name — or saving the page at all — does not silently wipe the secret.
    /// </summary>
    public async Task Handle(SetToolOutboundHeaderCommand request, CancellationToken cancellationToken)
    {
        await Manageable(request.ToolId, cancellationToken);

        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        var (_, existing) = await _secrets.GetOutboundHeader(request.ToolId, cancellationToken);
        var value = string.IsNullOrWhiteSpace(request.Value) ? existing : request.Value;

        // Clearing the name clears the pair: a value with nothing to send it under is dead weight
        // that would come back the moment someone typed a name again.
        await _secrets.SetOutboundHeader(request.ToolId, name, name is null ? null : value,
            cancellationToken);
    }

    /// <summary>
    ///     Stores the hash of the secret a maker's endpoint answers with. Clearing it un-verifies
    ///     the URL: without a secret there is nothing the endpoint could say that would prove
    ///     anything, so leaving deliveries flowing would be resting on a check we can no longer run.
    /// </summary>
    public async Task Handle(SetToolVerificationSecretCommand request, CancellationToken cancellationToken)
    {
        var tool = await Manageable(request.ToolId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Secret))
        {
            await _secrets.SetVerificationSecretHash(tool.Id, null, cancellationToken);
            tool.ClearWebhookVerification();
            await _tools.Save(tool, cancellationToken);
            return;
        }

        await _secrets.SetVerificationSecretHash(tool.Id, WebhookSecrets.HashOf(request.Secret),
            cancellationToken);

        // A new secret means the endpoint has to prove itself again — the old proof was against a
        // value their handler no longer returns.
        tool.ClearWebhookVerification();
        await _tools.Save(tool, cancellationToken);
    }

    /// <summary>
    ///     Asks the endpoint to answer with the maker's registered secret. Uses the same client, the
    ///     same timeout and the same outbound header a real delivery would — verifying under gentler
    ///     conditions than we deliver under proves nothing.
    /// </summary>
    public async Task<WebhookVerificationResult> Handle(VerifyToolWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var tool = await Manageable(request.ToolId, cancellationToken);
        if (tool.WebhookUrl is null)
            throw new ToolWebhookModeException("Set a webhook URL before verifying it.");

        // Re-checked rather than trusted: this URL may predate the rule, and verification is the
        // request that actually leaves our network.
        await CheckedTarget(tool.WebhookUrl.ToString(), cancellationToken);

        // Without a registered secret there is nothing an endpoint could say that would prove
        // anything, so this is unverifiable rather than unverified.
        var expected = await _secrets.GetVerificationSecretHash(tool.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(expected))
            throw new ToolWebhookModeException(
                "Set a verification secret first, and have your endpoint answer with it. We never " +
                "send it to your server — that is what makes answering with it proof.");

        var (headerName, headerValue) = await _secrets.GetOutboundHeader(tool.Id, cancellationToken);
        var outcome = await _client.Verify(tool.WebhookUrl, expected, headerName, headerValue,
            cancellationToken);

        if (!outcome.Succeeded)
            return new WebhookVerificationResult(false, outcome.Reason.ToString(), outcome.StatusCode,
                outcome.RemoteBodySnippet);

        tool.MarkWebhookVerified(_dateTime.Now);
        await _tools.Save(tool, cancellationToken);
        return new WebhookVerificationResult(true, null, outcome.StatusCode, null);
    }

    public async Task<Guid> Handle(CreateToolCommand request, CancellationToken cancellationToken)
    {
        // The same guard owning a community carries (delete-my-data §8.2, §8.3). Without it you
        // request deletion owning nothing, register a tool on day three, and it evaporates on day
        // seven taking its connected players with it.
        var pending = await _mediator.Send(new GetPendingAccountDeletionQuery(_currentUser.User.Id),
            cancellationToken);
        if (pending is not null)
            throw new ToolListingException(
                "Your account is scheduled for deletion, so you can't register a tool right now. " +
                "Cancel the deletion first if you've changed your mind.");

        // Rule 2's sanction. Deleting a tool never stopped its maker registering another thirty
        // seconds later, which is the entire reason the ban exists.
        if (await _bans.GetBan(_currentUser.User.Id, cancellationToken) is not null)
            throw new ToolListingException(
                "You can't register a tool. If you think that's wrong, ask in the PIU Scores Discord.");

        var tool = Tool.Create(Guid.NewGuid(), _currentUser.User.Id, Name.From(request.Name),
            _dateTime.Now, Link(request.RepositoryUrl), request.DiscordHandle, _dateTime.Now,
            request.Kind);
        await _tools.Save(tool, cancellationToken);

        // The maker is player one. Without this they cannot test their own tool against a real
        // account, and finding themselves in their own directory would be a silly first step.
        await _tools.GrantShare(tool.Id, _currentUser.User.Id, ShareSource.Direct, _dateTime.Now,
            cancellationToken);
        return tool.Id;
    }

    /// <summary>
    ///     Fetches the source repository anonymously and records whether it answered.
    ///     <para>
    ///         A failed check clears the previous proof rather than leaving it standing. A repository
    ///         that has gone private is exactly the case this exists to catch, and a stale tick
    ///         beside a dead link is worse than no tick at all.
    ///     </para>
    /// </summary>
    public async Task<RepositoryCheckResult> Handle(CheckToolRepositoryCommand request,
        CancellationToken cancellationToken)
    {
        var tool = await Manageable(request.ToolId, cancellationToken);
        if (tool.RepositoryUrl is null)
            throw ToolRepositoryRequiredException.ForMaker();

        var outcome = await _repositories.Check(tool.RepositoryUrl, cancellationToken);

        if (outcome.Reachable) tool.MarkRepositoryReachable(_dateTime.Now);
        else tool.ClearRepositoryCheck();

        await _tools.Save(tool, cancellationToken);

        return new RepositoryCheckResult(outcome.Reachable,
            outcome.Reachable ? null : outcome.Reason.ToString(), outcome.StatusCode);
    }

    public async Task Handle(UpdateToolCommand request, CancellationToken cancellationToken)
    {
        var tool = await Manageable(request.ToolId, cancellationToken);
        tool.Describe(Name.From(request.Name), request.Description, Link(request.Url),
            Link(request.RepositoryUrl));
        tool.SetDiscordHandle(request.DiscordHandle);
        await _tools.Save(tool, cancellationToken);
    }

    private static Uri? Link(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value);
    }

    public async Task Handle(SetToolAllToolsShareCommand request, CancellationToken cancellationToken)
    {
        var tool = await Manageable(request.ToolId, cancellationToken);
        tool.SetAcceptsAllToolsShare(request.Accepts);
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(SetToolWebhookCommand request, CancellationToken cancellationToken)
    {
        var tool = await Manageable(request.ToolId, cancellationToken);
        var connected = await _tools.CountConnectedPlayers(request.ToolId, cancellationToken);
        tool.SetWebhook(request.Mode,
            await CheckedTarget(request.Url, cancellationToken), connected,
            hasOutboundHeader: !string.IsNullOrWhiteSpace(
                (await _secrets.GetOutboundHeader(request.ToolId, cancellationToken)).Name));
        tool.SetMixes(request.Mixes);
        await _tools.Save(tool, cancellationToken);
    }

    public async Task Handle(RequestToolListingCommand request, CancellationToken cancellationToken)
    {
        var tool = await Manageable(request.ToolId, cancellationToken);
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
        await Manageable(request.ToolId, cancellationToken);
        await _tools.DeleteTool(request.ToolId, cancellationToken);
    }

    public async Task<IReadOnlyList<ToolRecord>> Handle(GetMyToolsQuery request,
        CancellationToken cancellationToken)
    {
        var tools = await _tools.GetToolsOwnedBy(_currentUser.User.Id, cancellationToken);
        return await ProjectAll(tools, cancellationToken);
    }

    /// <summary>
    ///     Every tool, for the admin console. Kept separate from <see cref="GetMyToolsQuery" />
    ///     rather than widening it: "mine" has to keep meaning mine, or the player-facing My Tools
    ///     section would list the whole site back to the one person who is also an admin.
    /// </summary>
    public async Task<IReadOnlyList<ToolRecord>> Handle(GetAllToolsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.User.IsAdmin) return Array.Empty<ToolRecord>();

        var tools = await _tools.GetAllTools(cancellationToken);
        return await ProjectAll(tools, cancellationToken);
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

    private async Task<IReadOnlyList<ToolRecord>> ProjectAll(IReadOnlyList<Tool> tools,
        CancellationToken cancellationToken)
    {
        var keyCounts = await _tools.CountKeysFor(tools.Select(t => t.Id).ToArray(), _dateTime.Now,
            cancellationToken);
        return await Task.WhenAll(tools.Select(t => Project(t, cancellationToken, keyCounts)));
    }

    private async Task<ToolRecord> Project(Tool tool, CancellationToken cancellationToken,
        IReadOnlyDictionary<Guid, int>? keyCounts = null)
    {
        // Batched by the list handlers; a single-tool read pays for one extra query.
        var keyCount = keyCounts is not null && keyCounts.TryGetValue(tool.Id, out var n)
            ? n
            : keyCounts is not null
                ? 0
                : (await _tools.CountKeysFor(new[] { tool.Id }, _dateTime.Now, cancellationToken))
                .GetValueOrDefault(tool.Id);

        // Who registered it is the review queue's main signal, so it travels with the record
        // rather than being looked up per row on the page.
        var owner = await _users.GetUser(tool.OwnerUserId, cancellationToken);
        // The name travels; neither secret's value ever does. A maker who forgets one sets a new one.
        var (headerName, headerValue) = await _secrets.GetOutboundHeader(tool.Id, cancellationToken);
        var verificationHash = await _secrets.GetVerificationSecretHash(tool.Id, cancellationToken);
        return new ToolRecord(tool.Id, tool.OwnerUserId, owner?.Name.ToString() ?? string.Empty,
            tool.Name.ToString(), tool.Description,
            tool.Url?.ToString(), tool.Visibility, tool.AcceptsAllToolsShare, tool.WebhookMode,
            tool.WebhookUrl?.ToString(), tool.Mixes.ToArray(),
            await _tools.CountConnectedPlayers(tool.Id, cancellationToken),
            tool.CreatedAt, tool.ApprovedAt, tool.RejectionReason, tool.WebhookUrlVerifiedAt,
            headerName, !string.IsNullOrWhiteSpace(headerValue),
            !string.IsNullOrWhiteSpace(verificationHash),
            tool.RepositoryUrl?.ToString(), tool.RepositoryOwner, tool.RepositoryCheckedAt,
            tool.DiscordHandle, tool.AgreedToRulesAt, tool.CanBeSharedWithOthers, tool.Kind,
            keyCount > 0, tool.WebhookMode != WebhookMode.None);
    }

    /// <summary>
    ///     Parses and vets a webhook URL before it is stored, so a bad one is a refusal the maker can
    ///     read rather than an exception from inside HttpClient later.
    ///     <para>
    ///         The private-address check is on the <b>resolved</b> address: a hostname allowlist
    ///         proves nothing, because <c>tool.example</c> can resolve to 10.0.0.5.
    ///     </para>
    /// </summary>
    private async Task<Uri?> CheckedTarget(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || !WebhookTarget.HasUsableScheme(parsed))
            throw new ToolWebhookModeException(
                "A webhook URL has to start with https:// (or http://). Other schemes cannot be " +
                "delivered to.");

        if (_configuration.Value.AllowPrivateWebhookTargets) return parsed;

        if (WebhookTarget.HasPrivateHostname(parsed))
            throw new ToolWebhookModeException(PrivateTargetMessage);

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(parsed.Host, cancellationToken);
        }
        catch (Exception)
        {
            // A name that will not resolve is not a private-network probe; let verification report
            // DnsFailure in its own vocabulary rather than failing the save with a guess.
            return parsed;
        }

        if (addresses.Any(WebhookTarget.IsPrivate))
            throw new ToolWebhookModeException(PrivateTargetMessage);

        return parsed;
    }

    private const string PrivateTargetMessage =
        "That address is on a private network, so we won't deliver to it — from our servers it " +
        "would point at our own infrastructure rather than yours. Use a public hostname, or run " +
        "PIU Scores locally to develop against localhost.";

    /// <summary>
    ///     The tool, if the caller may act on it: its owner, or an admin.
    ///     <para>
    ///         Admins pass because the site's operator has to be able to fix a maker's tool without
    ///         asking them to — and could reach the row directly anyway. Worth being clear about what
    ///         that includes: minting a key for someone else's tool, which is a key that reads that
    ///         tool's players. Neither secret becomes readable, because neither is ever returned.
    ///     </para>
    ///     <para>
    ///         Not-yours and not-found deliberately answer the same way: a stranger probing ids
    ///         learns nothing about which ones exist.
    ///     </para>
    /// </summary>
    private async Task<Tool> Manageable(Guid toolId, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(toolId, cancellationToken)
                   ?? throw new ToolNotFoundException();
        if (tool.OwnerUserId != _currentUser.User.Id && !_currentUser.User.IsAdmin)
            throw new ToolNotFoundException();

        return tool;
    }

    private async Task<Tool> AdminOnly(Guid toolId, CancellationToken cancellationToken)
    {
        if (!_currentUser.User.IsAdmin) throw new ToolNotFoundException();

        return await _tools.GetTool(toolId, cancellationToken) ?? throw new ToolNotFoundException();
    }
}
