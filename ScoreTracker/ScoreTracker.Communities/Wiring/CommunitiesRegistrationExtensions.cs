using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Communities.Wiring;

public static class CommunitiesRegistrationExtensions
{
    /// <summary>
    ///     Wires the Community vertical. ICommunityRepository is vertical-internal — the
    ///     saga is its only consumer, so unlike the tournament ports it never needed to
    ///     stay public in Domain. Handlers are discovered by the host's MediatR assembly
    ///     scan; bus consumers are NOT — see <see cref="AddCommunitiesConsumers" />.
    /// </summary>
    public static IServiceCollection AddCommunities(this IServiceCollection services)
    {
        services.AddTransient<ICommunityRepository, EFCommunitiesRepository>();
        services.AddTransient<ICommunityReader, EFCommunitiesRepository>();
        services.AddTransient<IDiscordFeedSubscriptionRepository, EFDiscordFeedSubscriptionRepository>();
        services.AddTransient<IDiscordFeedReader, EFDiscordFeedSubscriptionRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddTransient<ICommunityHighlightRepository, EFCommunityHighlightRepository>();
        services.AddTransient<ICommunityHighlightCapturer, CommunityHighlightCapturer>();
        services.AddSingleton<IDbModelContribution, CommunitiesModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside
    ///     the host's AddMassTransit block. Guarded by the tripwire in VerticalBoundaryTests.
    ///     CommunitySaga fans six event streams (scores, ratings, titles, weekly progress,
    ///     profile updates, UCS placements) out to community Discord channels — if this
    ///     hook stops covering it, every community feed silently goes quiet.
    /// </summary>
    public static void AddCommunitiesConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<CommunitySaga>();
        configurator.AddConsumer<DiscordFeedSaga>();
        configurator.AddConsumer<AccountPurgeConsumer>();
        configurator.AddConsumer<CommunityHighlightSaga>();
        configurator.AddConsumer<CommunityHighlightPurgeConsumer>();
        configurator.AddConsumer<BackfillCommunityHighlightsConsumer>();
    }
}
