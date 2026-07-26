using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <param name="Holders">Holder count per title. A title nobody holds is absent, not zero.</param>
/// <param name="TrackedPlayers">
///     Players with any title at all — the population a rarity percentage is out of.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record TitleRarityRecord(IReadOnlyDictionary<Name, int> Holders, int TrackedPlayers)
{
    /// <summary>Share of tracked players holding a title, 0 through 1.</summary>
    public double ShareOf(Name title)
    {
        if (TrackedPlayers <= 0) return 0;
        return Holders.TryGetValue(title, out var count) ? count / (double)TrackedPlayers : 0;
    }
}
