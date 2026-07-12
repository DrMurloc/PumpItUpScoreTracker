using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Wiring;

public static class CatalogRegistrationExtensions
{
    /// <summary>
    ///     Wires the Catalog vertical (chart catalog implementation + randomizer; ADR-001
    ///     Q2/Q4/Q5). IChartRepository stays a shared Domain port (the app-wide catalog
    ///     read contract) with its implementation Catalog-internal. Handlers are discovered
    ///     by the host's MediatR assembly scan; the vertical has no bus consumers.
    /// </summary>
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        services.AddTransient<IChartRepository, EFChartRepository>();
        services.AddTransient<IRandomizerRepository, EFRandomizerRepository>();
        services.AddTransient<IExternalChartAliasRepository, EFExternalChartAliasRepository>();
        services.AddTransient<IChartSkillMetricRepository, EFChartSkillMetricRepository>();
        services.AddSingleton<IDbModelContribution, CatalogModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the
    ///     MassTransitDiscoversTheCatalogsInternalConsumers tripwire test.
    /// </summary>
    public static void AddCatalogConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<PiuCenterCrawlSaga>();
    }
}
