using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class RenderShareCardHandler : IRequestHandler<GetTierListShareCardQuery, byte[]>
{
    private readonly IShareCardRenderer _renderer;

    public RenderShareCardHandler(IShareCardRenderer renderer)
    {
        _renderer = renderer;
    }

    public Task<byte[]> Handle(GetTierListShareCardQuery request, CancellationToken cancellationToken)
    {
        return _renderer.RenderTierListCard(request.Card, cancellationToken);
    }
}
