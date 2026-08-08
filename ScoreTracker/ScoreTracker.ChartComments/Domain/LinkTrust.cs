namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Decides whether a link in a comment opens straight through or via the interstitial. Pure and
///     constructed per parse: the fixed list is compiled in, the tool hosts are handed in by the
///     caller from the memory-cached public-tools read.
///     <para>
///         The two lists match differently on purpose. A fixed domain matches on a <b>dot
///         boundary</b>, so <c>youtu.be</c> covers <c>www.youtu.be</c> but
///         <c>youtube.com.evil.tld</c> is a stranger. A tool matches its <b>exact host only</b>,
///         because a tool living at <c>tools.example.com</c> is not a reason to trust
///         <c>evil.example.com</c> — the maker of the first does not own the second.
///     </para>
/// </summary>
internal sealed class LinkTrust
{
    /// <summary>
    ///     Places a Pump It Up player is expected to be sent: this site, the two video hosts, the
    ///     two Reddit hosts, the pattern archive, the official site, and the step-data site.
    /// </summary>
    internal static readonly IReadOnlyList<string> KnownDomains = new[]
    {
        "piuscores.arroweclip.se",
        "youtube.com",
        "youtu.be",
        "reddit.com",
        "redd.it",
        "pumpout2020.anyhowstep.com",
        "piugame.com",
        "piucenter.com"
    };

    private readonly IReadOnlySet<string> _toolHosts;

    public LinkTrust(IEnumerable<string>? toolHosts = null)
    {
        _toolHosts = (toolHosts ?? Array.Empty<string>())
            .Select(Normalize)
            .Where(host => host.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Parses a candidate link. Returns null for anything that is not absolute http or https —
    ///     which is where <c>javascript:</c> and <c>data:</c> die, at parse time rather than at a
    ///     render-time check somebody can forget to write.
    /// </summary>
    public static Uri? TryParse(string candidate)
    {
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return null;

        return uri.Scheme is "http" or "https" ? uri : null;
    }

    public bool IsTrusted(Uri uri)
    {
        var host = Normalize(uri.Host);
        if (host.Length == 0) return false;
        if (_toolHosts.Contains(host)) return true;

        return KnownDomains.Any(domain => MatchesOnDotBoundary(host, domain));
    }

    // host == domain, or host ends with "." + domain. Never a bare EndsWith: that would hand
    // "notyoutube.com" the trust built up by "youtube.com".
    private static bool MatchesOnDotBoundary(string host, string domain)
    {
        if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)) return true;

        return host.Length > domain.Length + 1
               && host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
    }

    // A trailing dot is a legal fully-qualified host and would otherwise dodge every comparison.
    private static string Normalize(string host)
    {
        return host.Trim().TrimEnd('.').ToLowerInvariant();
    }
}
