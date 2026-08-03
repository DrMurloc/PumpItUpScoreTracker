using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     The values a code sample needs filled in.
///     <para>
///         Assembled here rather than in Web so a page never has to know which of them is a secret.
///         The key arrives as its visible tail and nothing else — a snippet a maker can copy has to
///         be pasteable in a screenshot, and a real key on screen is a real key in a screenshot.
///     </para>
/// </summary>
internal sealed class ToolCodeSampleSaga : IRequestHandler<GetToolCodeSamplesQuery, ToolCodeContext>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IToolKeyRepository _keys;
    private readonly IToolSecretReader _secrets;
    private readonly IToolRepository _tools;

    public ToolCodeSampleSaga(IToolRepository tools, IToolKeyRepository keys, IToolSecretReader secrets,
        ICurrentUserAccessor currentUser)
    {
        _tools = tools;
        _keys = keys;
        _secrets = secrets;
        _currentUser = currentUser;
    }

    public async Task<ToolCodeContext> Handle(GetToolCodeSamplesQuery request,
        CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(request.ToolId, cancellationToken);
        if (tool is null || (tool.OwnerUserId != _currentUser.User.Id && !_currentUser.User.IsAdmin))
            throw new ToolNotFoundException();

        // The newest live key, because that is the one a maker is about to paste into something.
        var key = (await _keys.GetKeys(request.ToolId, cancellationToken))
            .Where(k => k.RevokedAt is null)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefault();

        var (headerName, _) = await _secrets.GetOutboundHeader(request.ToolId, cancellationToken);

        return new ToolCodeContext(key?.Last4 ?? string.Empty, tool.WebhookUrl?.ToString(), headerName,
            tool.Mixes.ToArray());
    }
}
