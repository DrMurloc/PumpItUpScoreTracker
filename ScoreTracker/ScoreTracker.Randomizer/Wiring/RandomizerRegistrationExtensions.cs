using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Randomizer.Application;
using ScoreTracker.Randomizer.Domain;
using ScoreTracker.Randomizer.Infrastructure;

namespace ScoreTracker.Randomizer.Wiring;

public static class RandomizerRegistrationExtensions
{
    /// <summary>
    ///     Wires the Randomizer vertical (docs/design/randomizer-overhaul.md): draw
    ///     generation, saved randomizer settings, and — as the overhaul lands — draws,
    ///     tournament-scoped settings, and spectator state. Handlers are discovered by the
    ///     host's MediatR assembly scan; bus consumers are NOT — see
    ///     <see cref="AddRandomizerConsumers" />.
    /// </summary>
    public static IServiceCollection AddRandomizer(this IServiceCollection services)
    {
        services.AddTransient<IRandomizerRepository, EFRandomizerRepository>();
        services.AddTransient<IDrawRepository, EFDrawRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddSingleton<IDbModelContribution, RandomizerModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by AccountPurgeCoverageTests, which
    ///     resolves every vertical consumer against the host's real registration.
    /// </summary>
    public static void AddRandomizerConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<AccountPurgeConsumer>();
    }
}
