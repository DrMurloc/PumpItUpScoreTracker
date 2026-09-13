using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Seasons.Application;
using ScoreTracker.Seasons.Contracts;

namespace ScoreTracker.Seasons.Wiring;

/// <summary>
///     Wires the Seasons vertical (docs/design/seasons.md D24): the quarterly season row, the roll
///     that opens and seals it, and the season pages to come. The season rows themselves — a
///     player's seasonal bests, stats and folder levels — stay with the verticals that own those
///     tables under a <c>SeasonId</c>; this vertical owns the calendar. A consumer hook
///     (<c>AddSeasonsConsumers</c>, the WeeklyChallenge shape) arrives with the first consumer,
///     the roll, in slice 1b.
/// </summary>
public static class SeasonsRegistrationExtensions
{
    public static IServiceCollection AddSeasons(this IServiceCollection services)
    {
        services.AddSingleton<IDbModelContribution, SeasonsModelContribution>();
        // Per request, like the user accessor it reads: the flag or the admin (D27).
        services.AddScoped<ISeasonsUiGate, SeasonsUiGate>();
        return services;
    }
}
