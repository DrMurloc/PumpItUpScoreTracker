using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Authenticates the credentials against piugame's login (Mirror ACL) and returns the
///     account identity bundle. Throws InvalidCredentialException when piugame rejects them.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPiuGameAccountIdentityQuery(string Username, RedactedString Password)
    : IQuery<PiuGameAccountIdentity>
{
}
