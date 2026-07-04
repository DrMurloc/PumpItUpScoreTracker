namespace ScoreTracker.Domain.Exceptions;

/// <summary>
///     The credentials authenticated against the official PIU site, but the account has no
///     game profile/card associated yet — distinct from bad credentials
///     (InvalidCredentialException). This is everyone's launch-week state on a brand-new
///     mix's site, so login/import flows must not report it as a wrong password.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NoGameAccountAssociatedException : Exception
{
    public NoGameAccountAssociatedException() : base(
        "The account authenticated but has no game profile associated with it yet.")
    {
    }
}
