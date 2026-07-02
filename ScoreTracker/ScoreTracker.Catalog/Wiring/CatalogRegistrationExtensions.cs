using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IDbModelContribution, CatalogModelContribution>();
        return services;
    }
}
