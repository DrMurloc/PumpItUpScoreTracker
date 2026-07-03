using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Ucs.Application;
using ScoreTracker.Ucs.Domain;
using ScoreTracker.Ucs.Infrastructure;

namespace ScoreTracker.Ucs.Wiring;

public static class UcsRegistrationExtensions
{
    /// <summary>
    ///     Wires the UCS vertical: its internal port bindings and its contribution to the
    ///     shared EF model. Handlers are discovered by the host's MediatR assembly scan; bus
    ///     consumers are NOT — see <see cref="AddUcsConsumers" />.
    /// </summary>
    public static IServiceCollection AddUcs(this IServiceCollection services)
    {
        services.AddTransient<IUcsRepository, EFUcsRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddSingleton<IDbModelContribution, UcsModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside the
    ///     host's AddMassTransit block. Guarded by the consumer-discovery tripwire tests.
    /// </summary>
    public static void AddUcsConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<AccountPurgeConsumer>();
    }
}
