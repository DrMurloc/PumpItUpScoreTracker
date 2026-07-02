using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.PlayerProgress.Application;

namespace ScoreTracker.PlayerProgress.Wiring;

public static class PlayerProgressRegistrationExtensions
{
    /// <summary>
    ///     Wires the Player Progress vertical (ratings, titles, history, score quality,
    ///     Pumbility projections, recommendations). Its EF repositories stay in
    ///     ScoreTracker.Data transitionally (cross-vertical SQL joins onto its tables are
    ///     still being converted to contract reads), so the reflective AddInfrastructure
    ///     binding still covers the ports and nothing is bound here yet. Handlers are
    ///     discovered by the host's MediatR assembly scan; bus consumers are NOT - see
    ///     <see cref="AddPlayerProgressConsumers" />.
    /// </summary>
    public static IServiceCollection AddPlayerProgress(this IServiceCollection services)
    {
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
    }
}
