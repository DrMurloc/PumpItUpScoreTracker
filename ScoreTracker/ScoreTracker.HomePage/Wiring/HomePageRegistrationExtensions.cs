using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.HomePage.Wiring;

public static class HomePageRegistrationExtensions
{
    /// <summary>
    ///     Wires the HomePage vertical (dashboard layout persistence): its internal port
    ///     bindings and its contribution to the shared EF model. Handlers are discovered
    ///     by the host's MediatR assembly scan. No bus consumers — layout persistence is
    ///     synchronous CRUD.
    /// </summary>
    public static IServiceCollection AddHomePage(this IServiceCollection services)
    {
        services.AddSingleton<IDbModelContribution, HomePageModelContribution>();
        return services;
    }
}
