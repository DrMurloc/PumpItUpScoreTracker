using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Randomizer.Domain;
using ScoreTracker.Randomizer.Infrastructure;

namespace ScoreTracker.Randomizer.Wiring;

public static class RandomizerRegistrationExtensions
{
    /// <summary>
    ///     Wires the Randomizer vertical (docs/design/randomizer-overhaul.md): draw
    ///     generation, saved randomizer settings, and — as the overhaul lands — draws,
    ///     tournament-scoped settings, and spectator state. Handlers are discovered by the
    ///     host's MediatR assembly scan; the vertical has no bus consumers.
    /// </summary>
    public static IServiceCollection AddRandomizer(this IServiceCollection services)
    {
        services.AddTransient<IRandomizerRepository, EFRandomizerRepository>();
        services.AddTransient<IDrawRepository, EFDrawRepository>();
        services.AddSingleton<IDbModelContribution, RandomizerModelContribution>();
        return services;
    }
}
