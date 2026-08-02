using System.Net;
using System.Net.Sockets;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     Where a webhook is allowed to point.
///     <para>
///         Verification POSTs to whatever a maker types, <b>from our server</b>, which sits inside
///         our hosting network. Without this they can aim it at things they cannot reach themselves
///         — the cloud metadata endpoint, another host on our private network, our own app on
///         loopback. We never render the response body, but the failure reason and status code are
///         shown, and "DNS failure" versus "timeout" versus "404" is enough to map an internal
///         network one URL at a time. That is server-side request forgery, and the fix is to refuse
///         the target rather than to hide the answer.
///     </para>
///     <para>
///         The check is on the <b>resolved</b> address, not the string: <c>tool.example</c> can
///         resolve to 10.0.0.5, so a hostname allowlist proves nothing. It is not proof against DNS
///         rebinding — that needs a socket-level callback on the handler, which is more machinery
///         than this is worth today.
///     </para>
/// </summary>
internal static class WebhookTarget
{
    /// <summary>
    ///     https, or http. Anything else — <c>ftp://</c>, <c>file://</c> — parses as a perfectly good
    ///     <see cref="Uri" /> and then throws <c>NotSupportedException</c> from inside HttpClient,
    ///     which is framework text on a maker's screen rather than something they can act on.
    /// </summary>
    public static bool HasUsableScheme(Uri url)
    {
        return url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp;
    }

    /// <summary>
    ///     Loopback, link-local (169.254/16 — the cloud metadata range), unique-local IPv6, and the
    ///     RFC1918 private ranges.
    /// </summary>
    public static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
            // fc00::/7, the IPv6 equivalent of RFC1918.
            var v6 = address.GetAddressBytes();
            if ((v6[0] & 0xFE) == 0xFC) return true;
            if (address.IsIPv4MappedToIPv6) return IsPrivate(address.MapToIPv4());
            return false;
        }

        var b = address.GetAddressBytes();
        return b[0] switch
        {
            10 => true,
            127 => true,
            169 when b[1] == 254 => true,
            172 when b[1] >= 16 && b[1] <= 31 => true,
            192 when b[1] == 168 => true,
            0 => true,
            _ => false
        };
    }

    /// <summary>Names that resolve inside a private zone often enough to be worth refusing by name.</summary>
    public static bool HasPrivateHostname(Uri url)
    {
        var host = url.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
    }
}
