using ScoreTracker.Catalog.Contracts;

namespace ScoreTracker.Catalog.Domain;

internal interface IAvatarRepository
{
    Task<IReadOnlyList<AvatarRecord>> GetAvatars(CancellationToken cancellationToken = default);
}
