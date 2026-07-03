using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure;

namespace ScoreTracker.PlayerProgress.Wiring;

public static class PlayerProgressRegistrationExtensions
{
    /// <summary>
    ///     Wires the Player Progress vertical (ratings, titles, history, score quality,
    ///     Pumbility projections, recommendations). The stats/title/history ports stay
    ///     public in Domain (Web pages and the Ledger wipe flow inject them); the EF
    ///     implementations are vertical-internal. Handlers are discovered by the host's
    ///     MediatR assembly scan; bus consumers are NOT - see
    ///     <see cref="AddPlayerProgressConsumers" />.
    /// </summary>
    public static IServiceCollection AddPlayerProgress(this IServiceCollection services)
    {
        services.AddTransient<IPlayerStatsRepository, EFPlayerStatsRepository>();
        services.AddTransient<IPlayerStatsReader, EFPlayerStatsRepository>();
        services.AddTransient<IPlayerHistoryRepository, EFPlayerHistoryRepository>();
        services.AddTransient<ITitleRepository, EFTitleRepository>();
        services.AddTransient<IFeedbackRepository, EFFeedbackRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddSingleton<IDbModelContribution, PlayerProgressModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook - call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    ///     PlayerRatingSaga is the rating pipeline; if this hook stops covering it, stats
    ///     and Pumbility silently stop updating after score imports.
    /// </summary>
    public static void AddPlayerProgressConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<PlayerRatingSaga>();
        configurator.AddConsumer<TitleSaga>();
        configurator.AddConsumer<PlayerHistorySaga>();
        configurator.AddConsumer<AccountPurgeConsumer>();
    }
}
