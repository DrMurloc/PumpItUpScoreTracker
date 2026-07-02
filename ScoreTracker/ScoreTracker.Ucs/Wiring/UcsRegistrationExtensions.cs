using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Ucs.Domain;
using ScoreTracker.Ucs.Infrastructure;

namespace ScoreTracker.Ucs.Wiring;

public static class UcsRegistrationExtensions
{
    /// <summary>
    ///     Wires the UCS vertical: its internal port bindings and its contribution to the
    ///     shared EF model. Handlers are discovered by the host's MediatR assembly scan.
    /// </summary>
    public static IServiceCollection AddUcs(this IServiceCollection services)
    {
        services.AddTransient<IUcsRepository, EFUcsRepository>();
        services.AddSingleton<IDbModelContribution, UcsModelContribution>();
        return services;
    }
}
