using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <param name="Holders">Public holders only, strongest paragon first, then by name.</param>
/// <param name="HiddenCount">
///     Holders whose profile is private. Surfaced as a count so the drawer can say why its list
///     is shorter than the rarity figure beside it, without naming anyone.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record TitleHoldersRecord(IReadOnlyList<TitleHolder> Holders, int HiddenCount);

[ExcludeFromCodeCoverage]
public sealed record TitleHolder(User User, ParagonLevel ParagonLevel);
