using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The Personalized Breakdown query (breakdown-page workshop): runs the same
///     <see cref="TierListBlendBuilder" /> computation as the blend and returns its
///     internals — per-chart source categories, pooled skill deviations, source
///     statuses — so the page explains exactly the list the player is looking at.
///     The community column is the stored sources combined alone (identical math to
///     the non-personalized blend), which makes personalized-vs-community a pure
///     per-chart diff.
/// </summary>
internal sealed class PersonalizedBreakdownHandler
    : IRequestHandler<GetPersonalizedTierListBreakdownQuery, PersonalizedTierListBreakdown>
{
    private static readonly string[] PersonalizingLenses = { "Pass", "Score" };

    private readonly TierListBlendBuilder _builder;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserAccessor _currentUser;

    public PersonalizedBreakdownHandler(IMediator mediator, IChartRepository charts, IScoreReader scores,
        IPlayerStatsReader playerStats, IUserTierListRepository userTierLists,
        ICurrentUserAccessor currentUser, IMemoryCache cache, IDateTimeOffsetAccessor clock,
        IScoreProjector projector)
    {
        _builder = new TierListBlendBuilder(mediator, charts, scores, playerStats, userTierLists, clock,
            projector);
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<PersonalizedTierListBreakdown> Handle(GetPersonalizedTierListBreakdownQuery request,
        CancellationToken cancellationToken)
    {
        var lens = request.Lens.ToString();
        if (!PersonalizingLenses.Contains(lens, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(request.Lens), lens,
                "Only the Pass and Score lenses personalize");

        var userId = request.UserId ?? _currentUser.User.Id;
        var cacheKey =
            $"{nameof(PersonalizedBreakdownHandler)}_{request.Mix}_{lens}_{request.ChartType}_{request.Level}_{userId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return await Build(request, lens, userId, cancellationToken);
        }) ?? throw new InvalidOperationException("Personalized breakdown could not be built");
    }

    private async Task<PersonalizedTierListBreakdown> Build(GetPersonalizedTierListBreakdownQuery request,
        string lens, Guid userId, CancellationToken cancellationToken)
    {
        var computation = await _builder.Compute(request.ChartType, request.Level, lens, userId, request.Mix,
            cancellationToken);
        // The community column is the Community VIEW, computed as such, rather than the
        // personalized recipe filtered down to its stored sources. Since Score's personalized
        // recipe is the projection alone, that filter now yields nothing — the column, and with
        // it the whole moved-charts diff this page exists for, would have gone blank. Two
        // computations on a page that is cached for six hours and read far less than the tier
        // list itself.
        var community = await _builder.Compute(request.ChartType, request.Level, lens, null, request.Mix,
            cancellationToken);

        var charts = computation.FolderCharts
            .Select(c => new BreakdownChartRecord(
                c.Id,
                TierListBlendBuilder.Combine("Community", c.Id, community.Sources, community.Modifiers)
                    .Category,
                TierListBlendBuilder.Combine("Final", c.Id, computation.Sources, computation.Modifiers)
                    .Category,
                CategoryFor(computation.Skill?.Entries, c.Id),
                CategoryFor(computation.Similar?.Entries, c.Id),
                CategoryFor(computation.Projection?.Entries, c.Id)))
            .ToArray();

        var skills = (computation.Skill?.PooledSkills ?? new Dictionary<Skill, SkillEvidence>())
            .Select(kv => new BreakdownSkillRecord(kv.Key, kv.Value.Deviation, kv.Value.Evidence,
                kv.Value.Usable))
            .ToArray();

        return new PersonalizedTierListBreakdown(
            charts,
            skills,
            computation.Skill?.Active ?? false,
            skills.Count(s => s.Usable),
            computation.Skill?.ScoredChartCount ?? 0,
            computation.Skill?.OutdatedScoreCount ?? 0,
            computation.Similar?.NeighborCount ?? 0,
            // How much of YOUR list is community, which is 0 on Score — not the weight of the
            // community column, which is computed separately and exists only to diff against.
            TierListBlendBuilder.CommunityWeightIn(computation.Modifiers),
            computation.Modifiers.GetValueOrDefault("Skill"),
            computation.Modifiers.GetValueOrDefault("Similar Players"),
            computation.Modifiers.GetValueOrDefault("Projection"),
            computation.Projection?.ProjectedChartCount ?? 0,
            computation.Projection?.FolderChartCount ?? computation.FolderCharts.Count,
            computation.IsProvisionalFallback);
    }

    private static TierListCategory CategoryFor(IReadOnlyDictionary<Guid, SongTierListEntry>? entries,
        Guid chartId)
    {
        return entries != null && entries.TryGetValue(chartId, out var entry)
            ? entry.Category
            : TierListCategory.Unrecorded;
    }
}
