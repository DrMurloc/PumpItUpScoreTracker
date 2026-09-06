using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure;

namespace ScoreTracker.EventCompetition.Wiring;

public static class EventCompetitionRegistrationExtensions
{
    /// <summary>
    ///     Wires the Event Competition vertical (tournaments, qualifiers, March of Murlocs).
    ///     ITournamentRepository stays a shared Domain port; IQualifiersRepository moved into
    ///     this vertical once the pages stopped injecting it (docs/design/qualifiers-overhaul.md)
    ///     transitionally — several Competition pages still inject them directly; the
    ///     implementations are vertical-internal. Handlers are discovered by the
    ///     host's MediatR assembly scan; bus consumers are NOT — see
    ///     <see cref="AddEventCompetitionConsumers" />.
    /// </summary>
    public static IServiceCollection AddEventCompetition(this IServiceCollection services)
    {
        services.AddTransient<ITournamentRepository, EFTournamentRepository>();
        services.AddTransient<IMoMRepository, EFMoMRepository>();
        services.AddTransient<IMoMReadRepository, EFMoMReadRepository>();
        services.AddTransient<IQualifiersRepository, EFQualifiersRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddTransient<IContributionDeletionRepository, EFContributionDeletionRepository>();
        services.AddSingleton<IDbModelContribution, EventCompetitionModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddEventCompetitionConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<ContributionsDeletionConsumer>();
        configurator.AddConsumer<QualifiersSaga>();
        configurator.AddConsumer<MarchOfMurlocsHandler>();
        configurator.AddConsumer<AccountPurgeConsumer>();
    }
}
