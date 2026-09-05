using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Application;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.Rivals.Infrastructure;

namespace ScoreTracker.Rivals.Wiring;

public static class RivalsRegistrationExtensions
{
    /// <summary>
    ///     Wires the Rivals vertical (docs/design/rivals.md §4). Every port here is
    ///     vertical-internal — nothing outside reads a rival edge except through the
    ///     published contract queries. Handlers are discovered by the host's MediatR
    ///     assembly scan; bus consumers are NOT — see <see cref="AddRivalsConsumers" />.
    /// </summary>
    public static IServiceCollection AddRivals(this IServiceCollection services)
    {
        services.AddTransient<IRivalRepository, EFRivalRepository>();
        services.AddTransient<IRivalInviteCodeRepository, EFRivalInviteCodeRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddTransient<RivalSubjectResolver>();
        // The published visibility port (docs/design/peers-abstraction.md §1): Rivals hosts the
        // implementation because it can see both non-public bases; consumers bind to the port.
        services.AddTransient<IPlayerVisibilityReader, PlayerVisibilityReader>();
        // The other peer-shaped Domain port (docs/design/peers-abstraction.md §4.2): where a score
        // stands among the peers a player chose. PlayerProgress reads it for the Hot Streak bar.
        services.AddTransient<IPeerStandingReader, PeerStandingReader>();
        services.AddTransient<RivalAdder>();
        services.AddTransient<RivalScoreReader>();
        services.AddSingleton<IDbModelContribution, RivalsModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    /// </summary>
    public static void AddRivalsConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<AccountPurgeConsumer>();
        // Without these two a stored board tag rots: it never becomes the account it
        // belongs to, and it never follows an accepted rename.
        configurator.AddConsumer<OfficialPlayerLinkSaga>();
        configurator.AddConsumer<OfficialPlayerRenameSaga>();
    }
}
