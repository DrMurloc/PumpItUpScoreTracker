using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     The two things that actually save a maker time: fire a real delivery on demand, and re-send
///     one that failed. The third — the signature echo — is a read and lives on the console saga.
/// </summary>
internal sealed class ToolDebugSaga :
    IRequestHandler<SendTestDeliveryCommand>,
    IRequestHandler<ReplayDeliveryCommand, bool>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IWebhookDeliveryDispatcher _dispatcher;
    private readonly IScoreReader _scores;
    private readonly IToolRepository _tools;
    private readonly IUserReader _users;

    public ToolDebugSaga(IToolRepository tools, IWebhookDeliveryDispatcher dispatcher,
        IWebhookDeliveryRepository deliveries, IUserReader users, IScoreReader scores,
        ICurrentUserAccessor currentUser)
    {
        _tools = tools;
        _dispatcher = dispatcher;
        _deliveries = deliveries;
        _users = users;
        _scores = scores;
        _currentUser = currentUser;
    }

    public async Task Handle(SendTestDeliveryCommand request, CancellationToken cancellationToken)
    {
        var tool = await Owned(request.ToolId, cancellationToken);
        if (tool.WebhookMode is WebhookMode.None or WebhookMode.PiuGameSession)
            throw new ToolWebhookModeException(
                "A test delivery needs a webhook mode that carries a body. Session mode has none — " +
                "we never write that body down, so there is nothing to send you out of band.");

        var me = _currentUser.User.Id;
        var user = await _users.GetUser(me, cancellationToken)
                   ?? throw new ToolNotFoundException();

        var changes = tool.WebhookMode == WebhookMode.ScorePush
            ? await BuildChanges(request, me, cancellationToken)
            : Array.Empty<DeliveryPayload.Change>();

        // The player is always the maker's own account. A test can never carry another player's
        // scores, whatever the maker asks for.
        var player = new DeliveryPayload.PlayerBlock(request.Mix.ToString(),
            DeliveryPayload.ScoringModelOf(request.Mix), me, user.Name.ToString(),
            user.GameTag?.ToString());

        await _dispatcher.Dispatch(tool, player, null, changes, hasMore: false, isTest: true,
            cancellationToken);
    }

    public async Task<bool> Handle(ReplayDeliveryCommand request, CancellationToken cancellationToken)
    {
        await Owned(request.ToolId, cancellationToken);

        var delivery = await _deliveries.Get(request.DeliveryRowId, cancellationToken);
        // Aged out, or a session delivery whose body was never written. Either way there is nothing
        // to re-send, and saying so beats pretending it worked.
        if (delivery is null || delivery.ToolId != request.ToolId || delivery.Body is null) return false;

        return await _dispatcher.Attempt(delivery.Id, cancellationToken);
    }

    /// <summary>
    ///     Real scores where possible. A maker is a player too, so their own last import is the most
    ///     realistic body available and costs one query — a synthetic batch is the fallback for
    ///     someone who would rather not use their own data, or who wants a specific size.
    /// </summary>
    private async Task<IReadOnlyList<DeliveryPayload.Change>> BuildChanges(SendTestDeliveryCommand request,
        Guid userId, CancellationToken cancellationToken)
    {
        if (request.UseMyLastImport)
        {
            // Through the published IScoreReader port — a vertical does not reference ScoreLedger.
            var mine = (await _scores.GetBestScores(request.Mix, userId, cancellationToken))
                .OrderByDescending(r => r.RecordedDate)
                .Take(Math.Min(WebhookDeliverySaga.MaxChangesPerDelivery, 25))
                .Select(r => new DeliveryPayload.Change(r.ChartId, false, null, r.Score,
                    null, null, r.Plate?.ToString(), r.IsBroken))
                .ToArray();

            if (mine.Length > 0) return mine;
        }

        var count = Math.Clamp(request.SyntheticCount, 1, WebhookDeliverySaga.MaxChangesPerDelivery);
        return Enumerable.Range(0, count)
            .Select(i => new DeliveryPayload.Change(Guid.Empty, i % 3 == 0, 900000, 950000 + i,
                null, null, "FairGame", false))
            .ToArray();
    }

    private async Task<Tool> Owned(Guid toolId, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(toolId, cancellationToken);
        if (tool is null || tool.OwnerUserId != _currentUser.User.Id) throw new ToolNotFoundException();

        return tool;
    }
}
