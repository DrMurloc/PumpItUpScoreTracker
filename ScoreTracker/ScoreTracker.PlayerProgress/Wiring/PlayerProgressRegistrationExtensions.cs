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
        services.AddTransient<IScoreHighlightRepository, EFScoreHighlightRepository>();
        services.AddTransient<IPlayerMilestoneRepository, EFPlayerMilestoneRepository>();
        services.AddTransient<IPlayerSeasonRecapRepository, EFPlayerSeasonRecapRepository>();
        services.AddTransient<CohortScoreProvider>();
        services.AddSingleton<IDbModelContribution, PlayerProgressModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook - call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddPlayerProgressConsumers(this IRegistrationConfigurator configurator)
    {
        // PlayerRatingSaga and TitleSaga run their score-batch work as in-process steps
        // of the HighlightCaptureSaga orchestration (revision 2) — as bus consumers they
        // only handle UserCreated and TitlesDetected respectively.
        configurator.AddConsumer<PlayerRatingSaga>();
        configurator.AddConsumer<TitleSaga>();
        configurator.AddConsumer<PlayerHistorySaga>();
        configurator.AddConsumer<AccountPurgeConsumer>();
        // The session-snapshot orchestrator: consumes every score batch, runs the
        // rating/title steps, publishes ScoreHighlightsCapturedEvent — if this
        // registration drops, stats, Pumbility, titles, AND the Discord cards all
        // silently stop updating after score imports.
        configurator.AddConsumer<HighlightCaptureSaga>();
        // Season recaps: admin-triggered, one user or the full sweep.
        configurator.AddConsumer<RecapSaga>();
    }
}
