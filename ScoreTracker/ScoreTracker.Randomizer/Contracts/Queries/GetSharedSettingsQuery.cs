using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Queries
{
    /// <summary>
    ///     Resolves a settings share token to its settings for preview/import. Null =
    ///     unknown token (revoked or mistyped).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetSharedSettingsQuery(Guid ShareToken) : IQuery<SavedRandomizerSettings?>
    {
    }
}
