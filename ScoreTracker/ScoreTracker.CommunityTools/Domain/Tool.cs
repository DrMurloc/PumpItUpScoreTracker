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
        string? rejectionReason, DateTimeOffset? webhookUrlVerifiedAt, Uri? repositoryUrl,
        string? repositoryOwner, DateTimeOffset? repositoryCheckedAt, string? discordHandle,
        DateTimeOffset? agreedToRulesAt, ToolKind kind)
    {
        Kind = kind;
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
        RepositoryUrl = repositoryUrl;
        RepositoryOwner = repositoryOwner;
        RepositoryCheckedAt = repositoryCheckedAt;
        DiscordHandle = discordHandle;
        AgreedToRulesAt = agreedToRulesAt;
    }

    public Guid Id { get; }
    public Guid OwnerUserId { get; }

    /// <summary>Whether this tool reads scores at all. Stated at registration, never derived.</summary>
    public ToolKind Kind { get; }
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

    /// <summary>Where a player reads this tool's source. Any public git host.</summary>
    public Uri? RepositoryUrl { get; private set; }

    /// <summary>
    ///     The account the repository sits under, parsed from the URL for the admin list. Shown,
    ///     never enforced on — a maker who pasted a repository they did not write is a judgement
    ///     only a human makes, and this is what makes it visible at a glance.
    /// </summary>
    public string? RepositoryOwner { get; private set; }

    /// <summary>
    ///     When <see cref="RepositoryUrl" /> last answered anonymously. Null means it has not, or the
    ///     URL changed since it did.
    /// </summary>
    public DateTimeOffset? RepositoryCheckedAt { get; private set; }

    /// <summary>
    ///     How the maker is reached when something breaks. Admin-visible only — never in a
    ///     player-facing record.
    /// </summary>
    public string? DiscordHandle { get; private set; }

    /// <summary>When the maker accepted the rules. Recorded once, at registration.</summary>
    public DateTimeOffset? AgreedToRulesAt { get; private set; }

    /// <summary>Session mode hands over a live credential, so it can never arrive by blanket consent.</summary>
    public bool RequiresExplicitShare => WebhookMode == WebhookMode.PiuGameSession;

    /// <summary>
    ///     Whether this tool may reach anyone but its own maker.
    ///     <para>
    ///         A tool failing this still works — keys mint, webhooks fire, and the maker is connected
    ///         to it as always. What it cannot do is acquire a second player. A configured repository
    ///         is a claim and a checked one is proof, exactly as with <see cref="CanDeliver" />: a
    ///         private repository answers 404 to the players it is supposed to be readable by, and
    ///         looks identical to a typo.
    ///     </para>
    ///     <para>
    ///         The handle is here for the same reason the repository is — a tool reading other
    ///         people's scores needs a maker who can be told when it goes wrong.
    ///     </para>
    /// </summary>
    public bool CanBeSharedWithOthers =>
        Shareable(Id, RepositoryUrl?.ToString(), RepositoryCheckedAt, DiscordHandle);

    /// <summary>
    ///     The same rule, over raw column values.
    ///     <para>
    ///         Effective read access is resolved in SQL against the entity, never against a rehydrated
    ///         aggregate, so without this the gate would have to be written twice — and the copy that
    ///         drifted would be the one actually deciding who can read a player's scores.
    ///     </para>
    /// </summary>
    public static bool Shareable(Guid id, string? repositoryUrl, DateTimeOffset? repositoryCheckedAt,
        string? discordHandle)
    {
        return GrandfatheredTools.Exempt(id)
               || (!string.IsNullOrWhiteSpace(repositoryUrl)
                   && repositoryCheckedAt is not null
                   && !string.IsNullOrWhiteSpace(discordHandle));
    }

    /// <summary>
    ///     Whether anything may actually be sent. A configured URL is a claim; a verified one is a
    ///     proof, and until we have the proof we are one typo away from posting a player's scores —
    ///     or in session mode their piugame credential — to a stranger's server.
    /// </summary>
    public bool CanDeliver => WebhookMode != WebhookMode.None
                              && WebhookUrl is not null
                              && WebhookUrlVerifiedAt is not null;

    /// <summary>
    ///     A new tool. The repository and handle are optional here on purpose — a maker building
    ///     against their own scores needs neither, and <see cref="CanBeSharedWithOthers" /> is what
    ///     holds the line once anyone else's data is involved.
    /// </summary>
    public static Tool Create(Guid id, Guid ownerUserId, Name name, DateTimeOffset createdAt,
        Uri? repositoryUrl = null, string? discordHandle = null, DateTimeOffset? agreedToRulesAt = null,
        ToolKind kind = ToolKind.Integrated)
    {
        return new Tool(id, ownerUserId, name, null, null, ToolVisibility.Private, true,
            WebhookMode.None, null, Array.Empty<MixEnum>(), createdAt, null, null, null,
            repositoryUrl, OwnerOf(repositoryUrl), null, Blank(discordHandle), agreedToRulesAt, kind);
    }

    public static Tool Rehydrate(Guid id, Guid ownerUserId, Name name, string? description, Uri? url,
        ToolVisibility visibility, bool acceptsAllToolsShare, WebhookMode webhookMode, Uri? webhookUrl,
        IEnumerable<MixEnum> mixes, DateTimeOffset createdAt, DateTimeOffset? approvedAt,
        string? rejectionReason, DateTimeOffset? webhookUrlVerifiedAt, Uri? repositoryUrl,
        string? repositoryOwner, DateTimeOffset? repositoryCheckedAt, string? discordHandle,
        DateTimeOffset? agreedToRulesAt, ToolKind kind)
    {
        return new Tool(id, ownerUserId, name, description, url, visibility, acceptsAllToolsShare,
            webhookMode, webhookUrl, mixes, createdAt, approvedAt, rejectionReason, webhookUrlVerifiedAt,
            repositoryUrl, repositoryOwner, repositoryCheckedAt, discordHandle, agreedToRulesAt, kind);
    }

    /// <summary>
    ///     Editing what players see returns a listed tool to review. A maker could otherwise pass
    ///     review as one thing and rename to another the next day, which is the whole point of the
    ///     approval step.
    ///     <para>
    ///         The repository counts as identity: it is printed beside the tool in the directory and
    ///         is the thing a player is invited to go and read. Passing review with a clean
    ///         repository and swapping it afterwards is the same trick as renaming, wearing a
    ///         different hat.
    ///     </para>
    ///     <para>
    ///         <b>A changed repository is an unchecked repository</b>, exactly as a changed webhook
    ///         URL is an unverified one. Without that, check once and swap to anything.
    ///     </para>
    /// </summary>
    public void Describe(Name name, string? description, Uri? url, Uri? repositoryUrl)
    {
        var identityChanged = name != Name || description != Description
                                           || url?.ToString() != Url?.ToString()
                                           || repositoryUrl?.ToString() != RepositoryUrl?.ToString();

        if (repositoryUrl?.ToString() != RepositoryUrl?.ToString())
        {
            RepositoryCheckedAt = null;
            RepositoryOwner = OwnerOf(repositoryUrl);
        }

        Name = name;
        Description = description;
        Url = url;
        RepositoryUrl = repositoryUrl;

        if (identityChanged && Visibility == ToolVisibility.Public)
        {
            Visibility = ToolVisibility.PendingApproval;
            ApprovedAt = null;
        }
    }

    /// <summary>Not player-visible, so changing it does not return the tool to review.</summary>
    public void SetDiscordHandle(string? handle)
    {
        DiscordHandle = Blank(handle);
    }

    /// <summary>Records that <see cref="RepositoryUrl" /> answered anonymously.</summary>
    public void MarkRepositoryReachable(DateTimeOffset at)
    {
        if (RepositoryUrl is null)
            throw new ToolRepositoryRequiredException("There is no repository link to check.");

        RepositoryCheckedAt = at;
    }

    /// <summary>Withdraws the proof after a check that did not answer.</summary>
    public void ClearRepositoryCheck()
    {
        RepositoryCheckedAt = null;
    }

    /// <summary>
    ///     The account a repository sits under — the first path segment, which holds for GitHub,
    ///     GitLab, Codeberg and gitea. It does not for sourcehut's <c>~user</c> or a nested GitLab
    ///     subgroup, and that is tolerable: nothing is decided on this value, it is printed for a
    ///     human to look at.
    /// </summary>
    private static string? OwnerOf(Uri? repositoryUrl)
    {
        var segment = repositoryUrl?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(segment) ? null : segment;
    }

    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public void RequestListing()
    {
        if (Visibility == ToolVisibility.Public) return;

        if (string.IsNullOrWhiteSpace(Description))
            throw new ToolListingException("A listed tool needs a description — it is what players read " +
                                           "before they decide to connect.");

        // A listing-only tool is nothing but a pointer, so the pointer has to point somewhere.
        if (Kind == ToolKind.ListingOnly && Url is null)
            throw new ToolListingException("A listing-only tool needs a link — it is the whole " +
                                           "thing a player is being sent to.");

        // Being listed is an invitation to every player on the site. The source they are invited to
        // read has to actually be readable, and someone has to be reachable when it goes wrong.
        if (!CanBeSharedWithOthers) throw ToolRepositoryRequiredException.ForMaker();

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

        // Entry-only, exactly like the zero-players rule above it. A tool already in session mode
        // can still be edited — PIU Tracker was seeded straight into this mode by migration and
        // would otherwise be uneditable by its own maker, including to fix a typo.
        if (enteringSessionMode && !hasOutboundHeader)
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
    ///     Records that the endpoint answered with the maker's registered secret. Only meaningful
    ///     for the URL that is set right now — <see cref="SetWebhook" /> clears it whenever that
    ///     changes.
    /// </summary>
    public void MarkWebhookVerified(DateTimeOffset at)
    {
        if (WebhookUrl is null)
            throw new ToolWebhookModeException("There is no webhook URL to verify.");

        WebhookUrlVerifiedAt = at;
    }

    /// <summary>
    ///     Withdraws the proof. Called when the secret it was proved against changes or is removed,
    ///     because a proof outliving the thing it was a proof of is worse than no proof at all.
    /// </summary>
    public void ClearWebhookVerification()
    {
        WebhookUrlVerifiedAt = null;
    }
}
