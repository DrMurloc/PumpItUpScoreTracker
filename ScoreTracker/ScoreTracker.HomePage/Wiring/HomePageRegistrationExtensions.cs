using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.HomePage.Application;
using ScoreTracker.HomePage.Domain;
using ScoreTracker.HomePage.Infrastructure;

namespace ScoreTracker.HomePage.Wiring;

public static class HomePageRegistrationExtensions
{
    /// <summary>
    ///     Wires the HomePage vertical (dashboard layout persistence): its internal port
    ///     bindings and its contribution to the shared EF model. Handlers are discovered
    ///     by the host's MediatR assembly scan; bus consumers are NOT — see
    ///     <see cref="AddHomePageConsumers" />.
    /// </summary>
    public static IServiceCollection AddHomePage(this IServiceCollection services)
    {
        services.AddTransient<IHomePageRepository, EFHomePageRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddSingleton<IDbModelContribution, HomePageModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by AccountPurgeCoverageTests, which
    ///     resolves every vertical consumer against the host's real registration.
    /// </summary>
    public static void AddHomePageConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<AccountPurgeConsumer>();
    }
}
