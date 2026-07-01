using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.OfficialMirror.Infrastructure.Apis;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Contracts;

namespace ScoreTracker.OfficialMirror.Wiring;

public static class OfficialMirrorRegistrationExtensions
{
    /// <summary>
    ///     Wires the Official Mirror vertical: the PiuGame ACL (typed HttpClient), its
    ///     internal ports, and its contribution to the shared EF model. Handlers are
    ///     discovered by the host's MediatR assembly scan; bus consumers are NOT — see
    ///     <see cref="AddOfficialMirrorConsumers" />.
    /// </summary>
    public static IServiceCollection AddOfficialMirror(this IServiceCollection services)
    {
        services.AddHttpClient<IPiuGameApi, PiuGameApi>(c =>
        {
            c.DefaultRequestHeaders.Add("Origin", "https://piugame.com");
        });
        services.AddTransient<IOfficialSiteClient, OfficialSiteClient>();
        services.AddTransient<IPiuTrackerClient, PiuTrackerClient>();
        services.AddTransient<IOfficialLeaderboardRepository, EFOfficialLeaderboardRepository>();
        services.AddTransient<IWorldRankingService, WorldRankingService>();
        services.AddSingleton<IDbModelContribution, OfficialMirrorModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddOfficialMirrorConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<OfficialLeaderboardSaga>();
    }
}
