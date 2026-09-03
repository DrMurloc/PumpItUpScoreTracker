namespace ScoreTracker.Identity.Contracts;

/// <param name="ImageUrl">The avatar being shown, whichever way it got there.</param>
/// <param name="IsPinned">The player chose it, so imports will not touch it.</param>
/// <param name="ImportedImageUrl">
///     What the last import saw. Equal to <paramref name="ImageUrl" /> when not pinned. Null only
///     for an account that has never imported and predates the backfill.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record MyAvatarRecord(Uri ImageUrl, bool IsPinned, Uri? ImportedImageUrl);
