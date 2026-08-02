using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     A community-built tool, and the rules about what it may become.
///     <para>
///         Rich rather than a property bag because its invariants are dense and dangerous to get
///         wrong: the listing flow, and the gate on entering PIUGame-session mode. Those rules live
///         here rather than in handlers for the same reason <c>Community</c>'s role rules do — a
///         handler is one caller, and the next caller will not remember.
///     </para>
/// </summary>
internal sealed class Tool
{
    private readonly HashSet<MixEnum> _mixes;

    private Tool(Guid id, Guid ownerUserId, Name name, string? description, Uri? url,
        ToolVisibility visibility, bool acceptsAllToolsShare, WebhookMode webhookMode, Uri? webhookUrl,
        IEnumerable<MixEnum> mixes, DateTimeOffset createdAt, DateTimeOffset? approvedAt,
        string? rejectionReason, DateTimeOffset? webhookUrlVerifiedAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
        Description = description;
        Url = url;
        Visibility = visibility;
        AcceptsAllToolsShare = acceptsAllToolsShare;
        WebhookMode = webhookMode;
        WebhookUrl = webhookUrl;
        _mixes = mixes.ToHashSet();
        CreatedAt = createdAt;
        ApprovedAt = approvedAt;
        RejectionReason = rejectionReason;
        WebhookUrlVerifiedAt = webhookUrlVerifiedAt;
    }

    public Guid Id { get; }
    public Guid OwnerUserId { get; }
    public Name Name { get; private set; }
    public string? Description { get; private set; }
    public Uri? Url { get; private set; }
    public ToolVisibility Visibility { get; private set; }
    public bool AcceptsAllToolsShare { get; private set; }
    public WebhookMode WebhookMode { get; private set; }
    public Uri? WebhookUrl { get; private set; }
    public IReadOnlySet<MixEnum> Mixes => _mixes;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>
    ///     When the maker last proved they control <see cref="WebhookUrl" />. Null means they have
    ///     not, or the URL changed since they did.
    /// </summary>
    public DateTimeOffset? WebhookUrlVerifiedAt { get; private set; }

    /// <summary>Session mode hands over a live credential, so it can never arrive by blanket consent.</summary>
    public bool RequiresExplicitShare => WebhookMode == WebhookMode.PiuGameSession;

    /// <summary>
    ///     Whether anything may actually be sent. A configured URL is a claim; a verified one is a
    ///     proof, and until we have the proof we are one typo away from posting a player's scores —
    ///     or in session mode their piugame credential — to a stranger's server.
    /// </summary>
    public bool CanDeliver => WebhookMode != WebhookMode.None
                              && WebhookUrl is not null
                              && WebhookUrlVerifiedAt is not null;

    public static Tool Create(Guid id, Guid ownerUserId, Name name, DateTimeOffset createdAt)
    {
        return new Tool(id, ownerUserId, name, null, null, ToolVisibility.Private, true,
            WebhookMode.None, null, Array.Empty<MixEnum>(), createdAt, null, null, null);
    }

    public static Tool Rehydrate(Guid id, Guid ownerUserId, Name name, string? description, Uri? url,
        ToolVisibility visibility, bool acceptsAllToolsShare, WebhookMode webhookMode, Uri? webhookUrl,
        IEnumerable<MixEnum> mixes, DateTimeOffset createdAt, DateTimeOffset? approvedAt,
        string? rejectionReason, DateTimeOffset? webhookUrlVerifiedAt)
    {
        return new Tool(id, ownerUserId, name, description, url, visibility, acceptsAllToolsShare,
            webhookMode, webhookUrl, mixes, createdAt, approvedAt, rejectionReason, webhookUrlVerifiedAt);
    }

    /// <summary>
    ///     Editing what players see returns a listed tool to review. A maker could otherwise pass
    ///     review as one thing and rename to another the next day, which is the whole point of the
    ///     approval step.
    /// </summary>
    public void Describe(Name name, string? description, Uri? url)
    {
        var identityChanged = name != Name || description != Description || url?.ToString() != Url?.ToString();
        Name = name;
        Description = description;
        Url = url;

        if (identityChanged && Visibility == ToolVisibility.Public)
        {
            Visibility = ToolVisibility.PendingApproval;
            ApprovedAt = null;
        }
    }

    public void RequestListing()
    {
        if (Visibility == ToolVisibility.Public) return;

        if (string.IsNullOrWhiteSpace(Description))
            throw new ToolListingException("A listed tool needs a description — it is what players read " +
                                           "before they decide to connect.");

        Visibility = ToolVisibility.PendingApproval;
        RejectionReason = null;
    }

    public void Approve(DateTimeOffset at)
    {
        if (Visibility != ToolVisibility.PendingApproval)
            throw new ToolListingException("Only a tool awaiting review can be approved.");

        Visibility = ToolVisibility.Public;
        ApprovedAt = at;
        RejectionReason = null;
    }

    public void Reject(string reason)
    {
        if (Visibility != ToolVisibility.PendingApproval)
            throw new ToolListingException("Only a tool awaiting review can be rejected.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ToolListingException("A rejection needs a reason the maker can act on.");

        Visibility = ToolVisibility.Rejected;
        RejectionReason = reason;
        ApprovedAt = null;
    }

    public void SetAcceptsAllToolsShare(bool accepts)
    {
        AcceptsAllToolsShare = accepts;
    }

    public void SetMixes(IEnumerable<MixEnum> mixes)
    {
        _mixes.Clear();
        foreach (var mix in mixes) _mixes.Add(mix);
    }

    /// <summary>
    ///     Changes delivery mode.
    ///     <para>
    ///         Moving between None, PlayerPing and ScorePush is free: none of them carries power the
    ///         tool's API key does not already have. Entering PIUGame-session mode requires
    ///         <paramref name="connectedPlayerCount" /> to be zero, because every player must agree to
    ///         that individually and the ones already connected agreed to something else — and an
    ///         outbound header, because we will not hand a live credential to an endpoint with no way
    ///         of telling our call from anyone else's.
    ///     </para>
    ///     <para>
    ///         <b>A changed URL is an unverified URL.</b> Without that, verify once and swap to
    ///         anything, which makes the whole handshake decorative.
    ///     </para>
    /// </summary>
    public void SetWebhook(WebhookMode mode, Uri? url, int connectedPlayerCount, bool hasOutboundHeader)
    {
        var enteringSessionMode = mode == Contracts.WebhookMode.PiuGameSession
                                  && WebhookMode != Contracts.WebhookMode.PiuGameSession;

        if (enteringSessionMode && connectedPlayerCount > 0)
            throw new ToolWebhookModeException(
                $"Switching to PIUGame session mode needs no connected players — {connectedPlayerCount} " +
                "already agreed to something else. They would each have to accept the new terms.");

        if (mode == Contracts.WebhookMode.PiuGameSession && !hasOutboundHeader)
            throw new ToolWebhookModeException(
                "PIUGame session mode needs an outbound header first. It is the only way your server " +
                "can tell our call from anyone else's, and this mode hands over a live piugame.com key.");

        if (mode != Contracts.WebhookMode.None && url is null)
            throw new ToolWebhookModeException("A delivery mode needs a URL to deliver to.");

        var target = mode == Contracts.WebhookMode.None ? null : url;
        if (target?.ToString() != WebhookUrl?.ToString()) WebhookUrlVerifiedAt = null;

        WebhookMode = mode;
        WebhookUrl = target;
    }

    /// <summary>
    ///     Records that the endpoint echoed our challenge back. Only meaningful for the URL that is
    ///     set right now — <see cref="SetWebhook" /> clears it whenever that changes.
    /// </summary>
    public void MarkWebhookVerified(DateTimeOffset at)
    {
        if (WebhookUrl is null)
            throw new ToolWebhookModeException("There is no webhook URL to verify.");

        WebhookUrlVerifiedAt = at;
    }
}
