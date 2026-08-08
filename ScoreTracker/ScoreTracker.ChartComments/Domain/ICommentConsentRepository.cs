namespace ScoreTracker.ChartComments.Domain;

/// <summary>What a player has agreed to. One row each, written the moment an agreement becomes true.</summary>
internal interface ICommentConsentRepository
{
    Task<CommentConsent?> GetFor(Guid userId, CancellationToken cancellationToken = default);

    Task Record(Guid userId, int termsVersion, bool consentedToPublicIdentity, DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     A player's standing agreements. <see cref="ConsentedToPublicIdentityAt" /> is null until a
///     private-profile player actually posts in public — the second checkbox appears when it is
///     true, not in advance.
/// </summary>
internal sealed record CommentConsent(
    DateTimeOffset AgreedToTermsAt,
    int TermsVersion,
    DateTimeOffset? ConsentedToPublicIdentityAt);
