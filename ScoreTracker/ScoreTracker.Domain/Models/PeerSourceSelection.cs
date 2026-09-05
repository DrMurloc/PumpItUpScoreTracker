namespace ScoreTracker.Domain.Models;

/// <summary>
///     Who a player chose to be measured against (docs/design/peers-abstraction.md D1, D2): any of
///     the four peer sources, ticked together — the peers are the union. One setting, sitewide;
///     each source resolves per mix and chart type when it is read.
///     <para>
///         Stored as the <see cref="SettingKey" /> UI setting, packed the way ShareCardOptions is:
///         a version token followed by the ticked sources, so a rolled-back release can read a
///         newer save and an unknown token is simply ignored. A missing setting is
///         <see cref="Default" /> (D20) — the competitive band alone, which is what every surface
///         showed before the player could choose.
///     </para>
/// </summary>
public sealed record PeerSourceSelection(
    bool Rivals,
    bool CompetitiveLevel,
    bool Pumbility,
    IReadOnlySet<Guid> CommunityIds)
{
    public const string SettingKey = "Universal__PeerSources";

    private const string Version = "v1";
    private const string RivalsToken = "Rivals";
    private const string CompetitiveToken = "Competitive";
    private const string PumbilityToken = "Pumbility";
    private const string CommunityPrefix = "Community:";

    private static readonly IReadOnlySet<Guid> NoCommunities = new HashSet<Guid>();

    /// <summary>The competitive band alone — today's page, for a player who never opened the dialog.</summary>
    public static PeerSourceSelection Default { get; } = new(false, true, false, NoCommunities);

    /// <summary>Nothing ticked: every score renders plain and the popover says why.</summary>
    public static PeerSourceSelection Nothing { get; } = new(false, false, false, NoCommunities);

    public bool Any => Rivals || CompetitiveLevel || Pumbility || CommunityIds.Count > 0;

    public string Serialize()
    {
        var tokens = new List<string> { Version };
        if (Rivals) tokens.Add(RivalsToken);
        if (CompetitiveLevel) tokens.Add(CompetitiveToken);
        if (Pumbility) tokens.Add(PumbilityToken);
        tokens.AddRange(CommunityIds.OrderBy(id => id).Select(id => CommunityPrefix + id.ToString("D")));
        return string.Join(',', tokens);
    }

    /// <summary>
    ///     A saved value with the version token and nothing else is a real choice — the player
    ///     un-ticked everything — and parses to <see cref="Nothing" />; a value without the version
    ///     token is not ours and parses to <see cref="Default" />.
    /// </summary>
    public static PeerSourceSelection Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return Default;
        var tokens = stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!tokens.Contains(Version, StringComparer.OrdinalIgnoreCase)) return Default;

        var communities = new HashSet<Guid>();
        var rivals = false;
        var competitive = false;
        var pumbility = false;
        foreach (var token in tokens)
        {
            if (token.Equals(RivalsToken, StringComparison.OrdinalIgnoreCase)) rivals = true;
            else if (token.Equals(CompetitiveToken, StringComparison.OrdinalIgnoreCase)) competitive = true;
            else if (token.Equals(PumbilityToken, StringComparison.OrdinalIgnoreCase)) pumbility = true;
            else if (token.StartsWith(CommunityPrefix, StringComparison.OrdinalIgnoreCase)
                     && Guid.TryParse(token[CommunityPrefix.Length..], out var id))
                communities.Add(id);
        }

        return new PeerSourceSelection(rivals, competitive, pumbility, communities);
    }
}
