using System.Text;

namespace ScoreTracker.Web.Security;

/// <summary>Which shape the Authorization header took.</summary>
public enum ApiCredentialKind
{
    /// <summary><c>Bearer …</c> — what the docs say a tool key is sent as.</summary>
    Bearer,

    /// <summary>
    ///     <c>Basic …</c> — a username nobody reads and a password that is either a personal token
    ///     or, because v1 taught every integrator that this is where a credential goes, a tool key.
    /// </summary>
    Basic
}

/// <summary>
///     The secret an Authorization header carried, read once and the same way everywhere. What the
///     secret means — a tool, a person, nothing — is the caller's decision; this only says what was
///     presented and why it could not be read when it could not.
///     <para>
///         Three places read the header: the v1 scheme, the v2 scheme and the rate limiter's
///         rejection hook, which runs before either scheme and still has to name the caller. One
///         parser is what keeps the three from disagreeing about a base64 edge case.
///     </para>
/// </summary>
public sealed class ApiCredential
{
    private ApiCredential(ApiCredentialKind kind, string secret, string? failure)
    {
        Kind = kind;
        Secret = secret;
        Failure = failure;
    }

    public ApiCredentialKind Kind { get; }

    /// <summary>The bearer token, or the Basic password. Empty when <see cref="Failure" /> is set.</summary>
    public string Secret { get; }

    /// <summary>Why the header could not be read, in the words the scheme fails with. Null when it could.</summary>
    public string? Failure { get; }

    public static ApiCredential Parse(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return Failed("Authorization header is empty");

        if (header.StartsWith("Bearer ", StringComparison.Ordinal))
            return new ApiCredential(ApiCredentialKind.Bearer, header["Bearer ".Length..].Trim(), null);

        if (!header.StartsWith("Basic ", StringComparison.Ordinal))
            return Failed("Authorization must be Bearer (tool key) or Basic (personal token)");

        // Personal tokens are unchanged from v1, down to the iso-8859-1 decode.
        string decoded;
        try
        {
            decoded = Encoding.GetEncoding("iso-8859-1")
                .GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        }
        catch (Exception)
        {
            return Failed("Could not decode credentials");
        }

        var split = decoded.Split(":");
        if (split.Length != 2) return Failed("Basic credentials must be username:password");

        return new ApiCredential(ApiCredentialKind.Basic, split[1], null);
    }

    private static ApiCredential Failed(string reason)
    {
        return new ApiCredential(ApiCredentialKind.Basic, string.Empty, reason);
    }
}
