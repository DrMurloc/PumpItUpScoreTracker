using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

/// <summary>
///     Whether one player may look at another, and on what bases: themselves, a public profile,
///     a user-created community they share (named, for the surface that shows why), or a rival
///     edge the viewer holds onto them. Produced by <see cref="Models.PlayerAudience.Describe" />.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerVisibility(
    bool CanView,
    bool IsYou,
    bool IsPublic,
    bool IsYourRival,
    IReadOnlyList<Name> SharedCommunities)
{
    public static PlayerVisibility Hidden { get; } = new(false, false, false, false, Array.Empty<Name>());

    /// <summary>A public player seen by someone with no relation to them — the anonymous case too.</summary>
    public static PlayerVisibility PublicOnly { get; } = new(true, false, true, false, Array.Empty<Name>());
}
