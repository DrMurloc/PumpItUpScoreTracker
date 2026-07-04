using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Authenticates the credentials against piugame's login (Mirror ACL) and returns the
///     account identity bundle. Throws InvalidCredentialException when piugame rejects the
///     credentials, NoGameAccountAssociatedException when they authenticate but no game
///     profile/card is associated yet. Mix defaults to Phoenix — /Login/PiuGame stays pinned
///     to Phoenix 1 as the identity source (locked decision).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPiuGameAccountIdentityQuery(string Username, RedactedString Password,
        MixEnum Mix = MixEnum.Phoenix)
    : IQuery<PiuGameAccountIdentity>
{
}
