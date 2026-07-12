using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure;

namespace ScoreTracker.ScoreLedger.Wiring;

public static class ScoreLedgerRegistrationExtensions
{
    /// <summary>
    ///     Wires the Score Ledger vertical: its internal port bindings, its published
    ///     <see cref="IScoreReader" /> read contract, and its contribution to the shared EF
    ///     model. Handlers are discovered by the host's MediatR assembly scan; bus consumers
    ///     are NOT — see <see cref="AddScoreLedgerConsumers" />.
    /// </summary>
    public static IServiceCollection AddScoreLedger(this IServiceCollection services)
    {
        services.AddTransient<IPhoenixRecordRepository, EFPhoenixRecordsRepository>();
        services.AddTransient<IScoreJournalRepository, EFScoreJournalRepository>();
        services.AddTransient<IXXChartAttemptRepository, EFXXChartAttemptRepository>();
        services.AddTransient<IScoreReader, EFPhoenixRecordsRepository>();
        services.AddTransient<IPhoenixRecordStatsRepository, EFPhoenixRecordStatsRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddTransient<ILedgerStatsRepository, EFLedgerStatsRepository>();
        // The Session Batcher: singleton so batch + session state survives across
        // handler instances (moved here from Web.Accessors — it has no ASP.NET
        // dependency and the Ledger owns the batching seam).
        services.AddSingleton<IPlayerScoreBatchAccumulator, PlayerScoreBatchAccumulator>();
        services.AddSingleton<IDbModelContribution, ScoreLedgerModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the
    ///     MassTransitDiscoversTheScoreLedgersInternalConsumers tripwire test.
    /// </summary>
    public static void AddScoreLedgerConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<UpdatePhoenixRecordHandler>();
        configurator.AddConsumer<AccountPurgeConsumer>();
    }
}
