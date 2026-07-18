using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        services.AddHttpClient<IPiuGameApi, PiuGameApi>((provider, c) =>
        {
            // Shared anonymous-scrape client. Its Origin stays the Phoenix host: the only
            // scheduled scrapes are Phoenix (P2 mirror deliberately unscheduled), and the
            // authenticated flows build their own per-mix client in GetSessionId, which sets
            // Origin from BaseUrlFor(mix).
            c.DefaultRequestHeaders.Add("Origin",
                provider.GetRequiredService<IOptions<PiuGameConfiguration>>().Value.BaseUrl);
        });
        services.AddTransient<IOfficialSiteClient, OfficialSiteClient>();
        services.AddTransient<IPiuTrackerClient, PiuTrackerClient>();
        services.AddTransient<IOfficialLeaderboardRepository, EFOfficialLeaderboardRepository>();
        services.AddTransient<IOfficialSnapshotRepository, EFOfficialSnapshotRepository>();
        services.AddTransient<IOfficialRecordRepository, EFOfficialRecordRepository>();
        services.AddTransient<IOfficialPlayerIdentityRepository, EFOfficialPlayerIdentityRepository>();
        // Singleton: the in-flight-import set is shared across every request and the bus consumer.
        services.AddSingleton<IImportConcurrencyGuard, ImportConcurrencyGuard>();
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
        configurator.AddConsumer<LeaderboardSweepSaga>();
        configurator.AddConsumer<PlayerIdentitySaga>();
        configurator.AddConsumer<RunOfficialImportConsumer>();
    }
}
