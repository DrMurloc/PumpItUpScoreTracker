using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;

namespace ScoreTracker.Catalog.Application;

internal sealed class GetAvatarCatalogHandler : IRequestHandler<GetAvatarCatalogQuery, IReadOnlyList<AvatarRecord>>
{
    private readonly IAvatarRepository _avatars;

    public GetAvatarCatalogHandler(IAvatarRepository avatars)
    {
        _avatars = avatars;
    }

    public Task<IReadOnlyList<AvatarRecord>> Handle(GetAvatarCatalogQuery request,
        CancellationToken cancellationToken)
    {
        return _avatars.GetAvatars(cancellationToken);
    }
}
