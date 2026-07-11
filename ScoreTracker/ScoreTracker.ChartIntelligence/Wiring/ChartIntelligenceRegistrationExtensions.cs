using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartIntelligence.Wiring;

public static class ChartIntelligenceRegistrationExtensions
{
    /// <summary>
    ///     Wires the Chart Intelligence vertical. Its four ports stay shared Domain ports
    ///     transitionally (Catalog's randomizer, the Mirror's tier-list population, and
    ///     several pages still inject them); the implementations are vertical-internal.
    ///     Handlers are discovered by the host's MediatR assembly scan; bus consumers are
    ///     NOT — see <see cref="AddChartIntelligenceConsumers" />.
    /// </summary>
    public static IServiceCollection AddChartIntelligence(this IServiceCollection services)
    {
        services.AddTransient<ITierListRepository, EFTierListRepository>();
        services.AddTransient<IChartDifficultyRatingRepository, EFChartDifficultyRatingRepository>();
        services.AddTransient<IChartScoringLevelRepository, EFChartScoringLevelRepository>();
        services.AddTransient<IChartPreferenceRepository, EFPreferenceRatingRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddTransient<IUserTierListRepository, EFUserTierListRepository>();
        services.AddTransient<IChartScoreStatsRepository, EFChartScoreStatsRepository>();
        services.AddSingleton<IDbModelContribution, ChartIntelligenceModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddChartIntelligenceConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<TierListSaga>();
        configurator.AddConsumer<ScoringDifficultySaga>();
        configurator.AddConsumer<AccountPurgeConsumer>();
        configurator.AddConsumer<UserTierListSaga>();
    }
}
