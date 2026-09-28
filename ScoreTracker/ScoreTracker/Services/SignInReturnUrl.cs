namespace ScoreTracker.Web.Services;

/// <summary>
///     The address a sign-in sends a visitor back to. Kept only when it is a path on this site, and
///     never when it is one of the sign-in pages themselves or the new-account step: a signed-in
///     visitor sent back to <c>/Login</c> is bounced to their return address again, which would be
///     <c>/Login</c>, and <c>/Setup</c>'s Continue resumes the return address, which would be itself.
///     <para>
///         The check is the one <c>IUrlHelper.IsLocalUrl</c> makes, written out so the setup page can
///         apply it from inside a circuit, where there is no URL helper to ask.
///     </para>
/// </summary>
public static class SignInReturnUrl
{
    public const string QueryKey = "returnUrl";

    private static readonly string[] SignInPaths = { "/Login", "/Welcome", "/PiuGameLogin", "/Setup" };

    public static string? Sanitize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsLocal(url)) return null;

        var path = url.Split('?', '#')[0].TrimEnd('/');
        return SignInPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)
                                    || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
            ? null
            : url;
    }

    /// <summary><paramref name="path" /> carrying the return address, or bare when there is none.</summary>
    public static string Append(string path, string? returnUrl)
    {
        return string.IsNullOrEmpty(returnUrl)
            ? path
            : $"{path}{(path.Contains('?') ? '&' : '?')}{QueryKey}={Uri.EscapeDataString(returnUrl)}";
    }

    /// <summary>
    ///     A path rooted on this site: <c>/</c> or <c>/somewhere</c>, never <c>//host</c> or <c>/\host</c>,
    ///     which a browser reads as another site, and nothing carrying a control character.
    /// </summary>
    private static bool IsLocal(string url)
    {
        if (url[0] != '/') return false;
        if (url.Length == 1) return true;
        if (url[1] == '/' || url[1] == '\\') return false;
        return !url.Any(char.IsControl);
    }
}
