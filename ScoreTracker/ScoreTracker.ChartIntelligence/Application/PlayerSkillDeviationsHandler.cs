using MediatR;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Publishes the Skill source's pooled evidence as a contract (Pumbility
///     projections v2 consumes it from PlayerProgress). Deviations convert from the
///     internal proficiency scale to score units at this boundary — consumers never
///     learn the floored-band internals.
/// </summary>
internal sealed class PlayerSkillDeviationsHandler
    : IRequestHandler<GetPlayerSkillDeviationsQuery, PlayerSkillDeviations>
{
    private readonly TierListBlendBuilder _builder;

    public PlayerSkillDeviationsHandler(IMediator mediator, IChartRepository charts, IScoreReader scores,
        IPlayerStatsReader playerStats, IUserTierListRepository userTierLists, IDateTimeOffsetAccessor clock)
    {
        _builder = new TierListBlendBuilder(mediator, charts, scores, playerStats, userTierLists, clock);
    }

    public async Task<PlayerSkillDeviations> Handle(GetPlayerSkillDeviationsQuery request,
        CancellationToken cancellationToken)
    {
        var evidence = await _builder.ComputeSkillEvidence(request.ChartType, request.AnchorLevel,
            request.Mix, request.UserId, Array.Empty<Guid>(), cancellationToken);

        var skills = evidence.PooledSkills.ToDictionary(kv => kv.Key, kv => new SkillDeviationRecord(
            kv.Value.Deviation * TierListBlendBuilder.SkillScoreRange,
            kv.Value.Evidence,
            kv.Value.Usable));

        return new PlayerSkillDeviations(skills,
            skills.Count(kv => kv.Value.Usable) >= TierListBlendBuilder.MinUsableSkills,
            evidence.ScoredChartCount);
    }
}
