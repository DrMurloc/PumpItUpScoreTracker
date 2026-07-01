using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Catalog.Wiring;

public static class CatalogRegistrationExtensions
{
    /// <summary>
    ///     Wires the Catalog vertical (currently the chart randomizer slice; content-write
    ///     flows follow — ADR-001 Q2/Q4). Handlers are discovered by the host's MediatR
    ///     assembly scan; the vertical has no bus consumers.
    /// </summary>
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        services.AddTransient<IRandomizerRepository, EFRandomizerRepository>();
        services.AddSingleton<IDbModelContribution, CatalogModelContribution>();
        return services;
    }
}
