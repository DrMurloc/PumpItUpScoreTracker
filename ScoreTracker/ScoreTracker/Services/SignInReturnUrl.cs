using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The address a sign-in sends a visitor back to. Kept only when it is a path on this site, and
///     never when it is one of the sign-in pages themselves: a signed-in visitor sent back to
///     <c>/Login</c> is bounced to their return address again, which would be <c>/Login</c>.
/// </summary>
public static class SignInReturnUrl
{
    public const string QueryKey = "returnUrl";

    private static readonly string[] SignInPaths = { "/Login", "/Welcome", "/PiuGameLogin" };

    public static string? Sanitize(string? url, IUrlHelper urls)
    {
        if (string.IsNullOrWhiteSpace(url) || !urls.IsLocalUrl(url)) return null;

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
}
