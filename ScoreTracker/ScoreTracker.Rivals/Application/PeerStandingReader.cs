using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     Rivals' implementation of the published <see cref="IPeerStandingReader" />
///     (docs/design/peers-abstraction.md §4.2), and the handler for the three peer-shaped contract
///     queries. This vertical hosts it because it is the one that can already see every source:
///     its own edges, the communities through <see cref="ICommunityReader" />, the competitive band
///     through <see cref="IPlayerStatsReader" />, and PUMBILITY peers through PlayerProgress's
///     contract. It moves with the peer abstraction when a Peers vertical exists.
///     <para>
///         Caching is where the 2026-07-10 incident lives: the competitive band is cached per mix,
///         type and half-level bucket so viewers share it; community members per community; and
///         the peers' scores per viewer, selection and chart, so a folder revisited costs nothing.
///         The subject's OWN bests are always read fresh — an import recolors the page at once.
///     </para>
/// </summary>
internal sealed class PeerStandingReader : IPeerStandingReader,
    IRequestHandler<GetPeerStandingsQuery, IReadOnlyDictionary<Guid, PeerStanding>>,
    IRequestHandler<GetMyPeerRosterQuery, PeerRoster>,
    IRequestHandler<GetPeerSourceCatalogQuery, PeerSourceCatalog>
{
    private const string WorldCommunity = "World";
    private static readonly TimeSpan BandTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ScoresTtl = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly ICommunityReader _communities;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly RivalSubjectResolver _resolver;
    private readonly RivalScoreReader _rivalScores;
    private readonly IRivalRepository _rivals;
    private readonly IScoreReader _scores;
    private readonly IPlayerStatsReader _stats;
    private readonly IUserReader _users;
    private readonly IPlayerVisibilityReader _visibility;

    public PeerStandingReader(IRivalRepository rivals, RivalSubjectResolver resolver, RivalScoreReader rivalScores,
        ICommunityReader communities, IPlayerStatsReader stats, IScoreReader scores, IChartRepository charts,
        IUserReader users, IPlayerVisibilityReader visibility, IMediator mediator,
        ICurrentUserAccessor currentUser, IMemoryCache cache)
    {
        _rivals = rivals;
        _resolver = resolver;
        _rivalScores = rivalScores;
        _communities = communities;
        _stats = stats;
        _scores = scores;
        _charts = charts;
        _users = users;
        _visibility = visibility;
        _mediator = mediator;
        _currentUser = currentUser;
        _cache = cache;
    }

    public Task<IReadOnlyDictionary<Guid, PeerStanding>> Handle(GetPeerStandingsQuery request,
        CancellationToken cancellationToken)
    {
        var viewer = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;
        var subject = request.SubjectUserId ?? viewer;
        if (subject == null) return Task.FromResult(Empty);
        // The peer choice is personal (D19): only the subject reads their own; anyone else looking
        // at them gets the competitive default, which is anonymous and public.
        var selection = subject == viewer ? null : PeerSourceSelection.Default;
        return GetStandings(subject.Value, request.Mix, request.ChartIds, selection, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, PeerStanding>> GetStandings(Guid userId, MixEnum mix,
        IReadOnlyCollection<Guid> chartIds, PeerSourceSelection? selection = null,
        CancellationToken cancellationToken = default)
    {
        // Era scores are letter grades and never rank on the million scale.
        if (chartIds.Count == 0 || mix.UsesLegacyScoring()) return Empty;
        selection ??= await ReadSelection(userId, cancellationToken);
        if (!selection.Any) return Empty;

        var bests = (await _scores.GetBestScores(mix, userId, cancellationToken))
            .Where(b => b.Score != null && !b.IsBroken && chartIds.Contains(b.ChartId))
            .ToDictionary(b => b.ChartId, b => (int)b.Score!.Value);
        if (bests.Count == 0) return Empty;

        var charts = (await _charts.GetCharts(mix, chartIds: bests.Keys, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var result = new Dictionary<Guid, PeerStanding>();
        foreach (var byType in charts.Values.GroupBy(c => c.Type))
        {
            var peers = await ResolvePeers(userId, mix, byType.Key, selection, cancellationToken);
            if (peers.Union.Count == 0)
            {
                foreach (var chart in byType)
                    result[chart.Id] = PeerStanding.NoCohort(0, 0, peers.Lines());
                continue;
            }

            var rows = await ReadRows(userId, mix, selection, peers, byType.Select(c => c.Id).ToArray(),
                cancellationToken);
            foreach (var chart in byType)
            {
                var chartRows = rows.GetValueOrDefault(chart.Id) ?? ChartRows.None;
                result[chart.Id] = PeerStandingCalculator.Compute(bests[chart.Id], chartRows.Passes,
                    chartRows.Broken, peers.Sources, peers.Union, chartRows.OfficialAsOf);
            }
        }

        return result;
    }

    public async Task<PeerRoster> Handle(GetMyPeerRosterQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return new PeerRoster(Array.Empty<PeerRosterEntry>(),
            Array.Empty<RivalSubject>(), 0, 0);
        var me = _currentUser.User.Id;
        var selection = await ReadSelection(me, cancellationToken);
        var myStats = await _stats.GetStats(request.Mix, me, cancellationToken);
        var myLevel = LevelOn(myStats, request.Dimension);
        if (!selection.Any) return new PeerRoster(Array.Empty<PeerRosterEntry>(), Array.Empty<RivalSubject>(), 0, myLevel);

        // The list is sorted on one dimension, so its band is that dimension's; the combined level
        // has no PUMBILITY pool of its own, so Combined draws both types' peers.
        var peers = await ResolvePeers(me, request.Mix, request.Dimension, selection, cancellationToken,
            forRoster: true);
        var siteIds = peers.Union.Where(id => !peers.GhostKeys.Contains(id)).ToArray();
        var users = siteIds.Length == 0
            ? new Dictionary<Guid, User>()
            : (await _users.GetUsers(siteIds, cancellationToken)).ToDictionary(u => u.Id);
        var audience = await _visibility.GetAudience(me, cancellationToken);
        var visible = users.Values.Where(u => audience.Describe(u.Id, u.IsPublic).CanView).ToArray();
        var levels = visible.Length == 0
            ? new Dictionary<Guid, PlayerStatsRecord>()
            : (await _stats.GetStats(request.Mix, visible.Select(u => u.Id), cancellationToken))
            .ToDictionary(s => s.UserId);

        var rows = visible
            .Select(u => new PeerRosterEntry(u,
                levels.TryGetValue(u.Id, out var s) ? LevelOn(s, request.Dimension) : 0,
                peers.Has(PeerSourceKind.Rivals, u.Id),
                peers.CommunityNames(u.Id),
                peers.Has(PeerSourceKind.CompetitiveLevel, u.Id),
                peers.Has(PeerSourceKind.Pumbility, u.Id)))
            .OrderBy(r => Math.Abs(r.Level - myLevel))
            .ThenBy(r => r.User.Name.ToString())
            .Take(request.Take)
            .ToArray();
        return new PeerRoster(rows, peers.Ghosts, peers.Union.Count, myLevel);
    }

    public async Task<PeerSourceCatalog> Handle(GetPeerSourceCatalogQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return PeerSourceCatalog.Empty;
        var me = _currentUser.User.Id;
        var options = new List<PeerSourceOption>();

        var rivals = await _resolver.Resolve(await _rivals.GetRivalsOwnedBy(me, cancellationToken), request.Mix,
            cancellationToken);
        var rivalIds = rivals.Where(r => r.UserId != null).Select(r => r.UserId!.Value).ToHashSet();
        options.Add(new PeerSourceOption(PeerSourceKind.Rivals, null, string.Empty, false, false, true, rivalIds,
            rivalIds, rivals.Count(r => r.IsGhost)));

        var stats = await _stats.GetStats(request.Mix, me, cancellationToken);
        options.Add(new PeerSourceOption(PeerSourceKind.CompetitiveLevel, null, string.Empty, false, false, true,
            await CompetitiveBand(request.Mix, ChartType.Single, stats, me, cancellationToken),
            await CompetitiveBand(request.Mix, ChartType.Double, stats, me, cancellationToken), 0));

        var pumbilityAvailable = request.Mix == MixEnum.Phoenix2;
        options.Add(new PeerSourceOption(PeerSourceKind.Pumbility, null, string.Empty, false, false,
            pumbilityAvailable,
            pumbilityAvailable ? await PumbilityPeers(request.Mix, ChartType.Single, me, cancellationToken) : NoIds,
            pumbilityAvailable ? await PumbilityPeers(request.Mix, ChartType.Double, me, cancellationToken) : NoIds,
            0));

        foreach (var community in (await _communities.GetUserCommunities(me, cancellationToken))
                 .OrderBy(c => IsWorld(c) ? 2 : c.IsRegional ? 1 : 0)
                 .ThenBy(c => c.CommunityName.ToString()))
        {
            var members = await CommunityMembers(community, me, cancellationToken);
            options.Add(new PeerSourceOption(PeerSourceKind.Community, community.CommunityId,
                community.CommunityName.ToString(), community.IsRegional, IsWorld(community), true, members, members,
                0));
        }

        return new PeerSourceCatalog(options);
    }

    // ---------------------------------------------------------------- resolution

    private static readonly IReadOnlyDictionary<Guid, PeerStanding> Empty = new Dictionary<Guid, PeerStanding>();
    private static readonly IReadOnlySet<Guid> NoIds = new HashSet<Guid>();

    private async Task<PeerSourceSelection> ReadSelection(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await _mediator.Send(new GetUserUiSettingsQuery(userId), cancellationToken);
        return PeerSourceSelection.Parse(settings.TryGetValue(PeerSourceSelection.SettingKey, out var stored)
            ? stored
            : null);
    }

    /// <summary>The resolved sources for one chart type, the subject removed from every set.</summary>
    private sealed class ResolvedPeers
    {
        public List<PeerStandingCalculator.SourceMembers> Sources { get; } = new();
        public HashSet<Guid> Union { get; } = new();
        public IReadOnlyList<RivalSubject> Ghosts { get; set; } = Array.Empty<RivalSubject>();
        public IReadOnlySet<Guid> GhostKeys => Ghosts.Select(g => g.EdgeId).ToHashSet();

        public bool Has(PeerSourceKind kind, Guid id) =>
            Sources.Any(s => s.Kind == kind && s.Members.Contains(id));

        public IReadOnlyList<string> CommunityNames(Guid id) =>
            Sources.Where(s => s.Kind == PeerSourceKind.Community && s.Members.Contains(id))
                .Select(s => s.CommunityName ?? string.Empty).ToArray();

        public IReadOnlyList<PeerStandingSource> Lines() =>
            Sources.Select(s => new PeerStandingSource(s.Kind, s.CommunityId, s.CommunityName, s.IsRegional,
                s.IsWorld, s.Members.Count, 0, 0, 0)).ToArray();

        public void Add(PeerStandingCalculator.SourceMembers source)
        {
            Sources.Add(source);
            Union.UnionWith(source.Members);
        }
    }

    /// <summary>
    ///     <paramref name="chartType" /> picks the band and the PUMBILITY pool; a null type is the
    ///     roster's Combined dimension. Co-op and the performance types have no competitive side, so
    ///     those two sources contribute nothing there and the rivals and communities carry the chart.
    /// </summary>
    private async Task<ResolvedPeers> ResolvePeers(Guid userId, MixEnum mix, ChartType? chartType,
        PeerSourceSelection selection, CancellationToken cancellationToken, bool forRoster = false)
    {
        var peers = new ResolvedPeers();
        if (selection.Rivals)
        {
            var rivals = await _resolver.Resolve(await _rivals.GetRivalsOwnedBy(userId, cancellationToken), mix,
                cancellationToken);
            peers.Ghosts = rivals.Where(r => r.IsGhost).ToArray();
            var members = rivals.Select(r => r.UserId ?? r.EdgeId).Where(id => id != userId).ToHashSet();
            peers.Add(new PeerStandingCalculator.SourceMembers(PeerSourceKind.Rivals, null, null, false, false,
                members));
        }

        if (selection.CommunityIds.Count > 0)
            foreach (var community in (await _communities.GetUserCommunities(userId, cancellationToken))
                     .Where(c => selection.CommunityIds.Contains(c.CommunityId))
                     .OrderBy(c => IsWorld(c) ? 2 : c.IsRegional ? 1 : 0)
                     .ThenBy(c => c.CommunityName.ToString()))
                peers.Add(new PeerStandingCalculator.SourceMembers(PeerSourceKind.Community, community.CommunityId,
                    community.CommunityName.ToString(), community.IsRegional, IsWorld(community),
                    await CommunityMembers(community, userId, cancellationToken)));

        var bandType = forRoster ? chartType : BandTypeFor(chartType);
        var hasBand = forRoster || bandType != null;
        if (selection.CompetitiveLevel && hasBand)
        {
            var stats = await _stats.GetStats(mix, userId, cancellationToken);
            peers.Add(new PeerStandingCalculator.SourceMembers(PeerSourceKind.CompetitiveLevel, null, null, false,
                false, await CompetitiveBand(mix, bandType, stats, userId, cancellationToken)));
        }

        // The PUMBILITY read is the viewer's own sweep (D31 leaves them out already), so it only
        // answers for the current user — which every caller that ticks it is.
        if (selection.Pumbility && mix == MixEnum.Phoenix2 && _currentUser.IsLoggedIn &&
            _currentUser.User.Id == userId && (forRoster || bandType != null))
        {
            var ids = new HashSet<Guid>();
            foreach (var type in bandType is { } one ? new[] { one } : new[] { ChartType.Single, ChartType.Double })
                ids.UnionWith(await PumbilityPeers(mix, type, userId, cancellationToken));
            peers.Add(new PeerStandingCalculator.SourceMembers(PeerSourceKind.Pumbility, null, null, false, false,
                ids));
        }

        return peers;
    }

    private static ChartType? BandTypeFor(ChartType? chartType) => chartType switch
    {
        ChartType.Single => ChartType.Single,
        ChartType.Double => ChartType.Double,
        _ => null
    };

    private static bool IsWorld(CommunityOverviewRecord community) =>
        community.CommunityName.ToString().Equals(WorldCommunity, StringComparison.OrdinalIgnoreCase);

    private static double LevelOn(PlayerStatsRecord stats, ChartType? dimension) => dimension switch
    {
        ChartType.Single => stats.SinglesCompetitiveLevel,
        ChartType.Double => stats.DoublesCompetitiveLevel,
        _ => stats.CompetitiveLevel
    };

    /// <summary>Half-level buckets, exactly as the retired cohort machinery shared them across viewers.</summary>
    private static double Bucket(double competitiveLevel) =>
        Math.Round(competitiveLevel * 2, MidpointRounding.AwayFromZero) / 2.0;

    private async Task<IReadOnlySet<Guid>> CompetitiveBand(MixEnum mix, ChartType? type, PlayerStatsRecord stats,
        Guid exclude, CancellationToken cancellationToken)
    {
        var bucket = Bucket(LevelOn(stats, type));
        if (bucket <= 0) return NoIds;
        var band = await _cache.GetOrCreateAsync($"{nameof(PeerStandingReader)}__Band__{mix}__{type}__{bucket}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = BandTtl;
                return (await _stats.GetPlayersByCompetitiveRange(mix, type, bucket, .5, cancellationToken))
                    .ToHashSet();
            }) ?? new HashSet<Guid>();
        return band.Contains(exclude) ? band.Where(id => id != exclude).ToHashSet() : band;
    }

    private async Task<IReadOnlySet<Guid>> PumbilityPeers(MixEnum mix, ChartType type, Guid exclude,
        CancellationToken cancellationToken)
    {
        return (await _mediator.Send(new GetPumbilityPeersQuery(type, mix), cancellationToken))
            .Where(id => id != exclude).ToHashSet();
    }

    private async Task<IReadOnlySet<Guid>> CommunityMembers(CommunityOverviewRecord community, Guid exclude,
        CancellationToken cancellationToken)
    {
        var members = await _cache.GetOrCreateAsync(
            $"{nameof(PeerStandingReader)}__Members__{community.CommunityId}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = BandTtl;
                return (await _communities.GetMembers(community.CommunityName, cancellationToken)).ToHashSet();
            }) ?? new HashSet<Guid>();
        return members.Contains(exclude) ? members.Where(id => id != exclude).ToHashSet() : members;
    }

    // ---------------------------------------------------------------- scores

    /// <summary>One chart's peer rows: the passes (site and board), and who only broke it.</summary>
    private sealed record ChartRows(IReadOnlyList<PeerStandingCalculator.PeerPass> Passes,
        IReadOnlySet<Guid> Broken, DateTimeOffset? OfficialAsOf)
    {
        public static ChartRows None { get; } =
            new(Array.Empty<PeerStandingCalculator.PeerPass>(), new HashSet<Guid>(), null);
    }

    /// <summary>
    ///     Cached per viewer, mix, selection and chart, so the read only reaches the ledger for the
    ///     charts a page has not seen lately — the shape that keeps a folder revisit free.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, ChartRows>> ReadRows(Guid userId, MixEnum mix,
        PeerSourceSelection selection, ResolvedPeers peers, IReadOnlyCollection<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        var selectionKey = selection.Serialize();
        var result = new Dictionary<Guid, ChartRows>();
        var missing = new List<Guid>();
        foreach (var chartId in chartIds)
            if (_cache.TryGetValue(RowsKey(userId, mix, selectionKey, chartId), out ChartRows? cached) && cached != null)
                result[chartId] = cached;
            else
                missing.Add(chartId);
        if (missing.Count == 0) return result;

        var siteIds = peers.Union.Where(id => !peers.GhostKeys.Contains(id)).ToArray();
        var passes = new Dictionary<Guid, List<PeerStandingCalculator.PeerPass>>();
        var broken = new Dictionary<Guid, HashSet<Guid>>();
        if (siteIds.Length > 0)
        {
            foreach (var score in await _scores.GetPlayerScores(mix, siteIds, missing, cancellationToken))
                Passes(passes, score.ChartId).Add(new PeerStandingCalculator.PeerPass(score.UserId, (int)score.Score, false));
            foreach (var (user, chart) in await _scores.GetBrokenBests(mix, siteIds, missing, cancellationToken))
                Broken(broken, chart).Add(user);
        }

        DateTimeOffset? asOf = null;
        if (peers.Ghosts.Count > 0)
        {
            // Site rivals are already in the union read; only the ghosts' board placements are new.
            var official = await _rivalScores.Read(peers.Ghosts, mix, missing, cancellationToken);
            asOf = official.OfficialAsOf;
            foreach (var (chartId, rows) in official.ByChart)
            foreach (var row in rows.Where(r => r.Source == RivalScoreSource.Official && !r.IsBroken))
                Passes(passes, chartId).Add(new PeerStandingCalculator.PeerPass(row.EdgeId, row.Score, true));
        }

        foreach (var chartId in missing)
        {
            var rows = new ChartRows(
                passes.TryGetValue(chartId, out var p) ? p : Array.Empty<PeerStandingCalculator.PeerPass>(),
                broken.TryGetValue(chartId, out var b) ? b : new HashSet<Guid>(),
                asOf);
            _cache.Set(RowsKey(userId, mix, selectionKey, chartId), rows, ScoresTtl);
            result[chartId] = rows;
        }

        return result;
    }

    private static List<PeerStandingCalculator.PeerPass> Passes(
        Dictionary<Guid, List<PeerStandingCalculator.PeerPass>> passes, Guid chartId)
    {
        if (!passes.TryGetValue(chartId, out var list)) passes[chartId] = list = new List<PeerStandingCalculator.PeerPass>();
        return list;
    }

    private static HashSet<Guid> Broken(Dictionary<Guid, HashSet<Guid>> broken, Guid chartId)
    {
        if (!broken.TryGetValue(chartId, out var set)) broken[chartId] = set = new HashSet<Guid>();
        return set;
    }

    private static string RowsKey(Guid userId, MixEnum mix, string selectionKey, Guid chartId) =>
        $"{nameof(PeerStandingReader)}__Rows__{userId}__{mix}__{selectionKey}__{chartId}";
}
