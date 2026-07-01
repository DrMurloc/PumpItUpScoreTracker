using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.WeeklyChallenge.Application;
using ScoreTracker.WeeklyChallenge.Infrastructure;

namespace ScoreTracker.WeeklyChallenge.Wiring;

public static class WeeklyChallengeRegistrationExtensions
{
    /// <summary>
    ///     Wires the Weekly Challenge vertical. IWeeklyTournamentRepository stays a shared
    ///     Domain port transitionally (RecommendedChartsSaga still reads it); the
    ///     implementation is vertical-internal. Handlers are discovered by the host's
    ///     MediatR assembly scan; bus consumers are NOT — see
    ///     <see cref="AddWeeklyChallengeConsumers" />.
    /// </summary>
    public static IServiceCollection AddWeeklyChallenge(this IServiceCollection services)
    {
        services.AddTransient<IWeeklyTournamentRepository, EFWeeklyTourneyRepository>();
        services.AddSingleton<IDbModelContribution, WeeklyChallengeModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddWeeklyChallengeConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<WeeklyTournamentSaga>();
    }
}
