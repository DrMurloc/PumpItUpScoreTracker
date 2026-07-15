using System.Diagnostics.CodeAnalysis;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Everything the static shell renders, resolved server-side from the request. Built by
///     <see cref="ShellModelFactory" />.
/// </summary>
/// <param name="DisplayName">Name, with the game tag appended when the user has one.</param>
/// <param name="ThemeMix">The /Account override applied to <paramref name="CurrentMix" />.</param>
/// <param name="ActivePath">Request path, for the bottom nav's active slot.</param>
/// <param name="ReturnUrl">Path and query, for endpoints that redirect back to where they were called from.</param>
[ExcludeFromCodeCoverage]
public sealed record ShellViewModel(
    bool IsLoggedIn,
    Guid? UserId,
    string? DisplayName,
    string AvatarUrl,
    MixEnum CurrentMix,
    MixEnum ThemeMix,
    bool IsGatedMix,
    bool HasRecap,
    IReadOnlyList<TournamentRecord> HighlightedEvents,
    string ActivePath,
    string ReturnUrl);
