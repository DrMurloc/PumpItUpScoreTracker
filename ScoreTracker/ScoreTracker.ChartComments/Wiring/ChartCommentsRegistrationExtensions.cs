using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.ChartComments.Application;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Wiring;

public static class ChartCommentsRegistrationExtensions
{
    /// <summary>
    ///     Wires the chart-comments vertical. Handlers are discovered by the host's MediatR
    ///     assembly scan; bus consumers are NOT — see <see cref="AddChartCommentsConsumers" />.
    /// </summary>
    public static IServiceCollection AddChartComments(this IServiceCollection services)
    {
        services.AddTransient<ICommentRepository, EFCommentRepository>();
        services.AddTransient<ICommentConsentRepository, EFCommentConsentRepository>();
        services.AddTransient<ICommentReportRepository, EFCommentReportRepository>();
        services.AddTransient<ICommentRestrictionRepository, EFCommentRestrictionRepository>();
        services.AddTransient<ICommentArchiveRepository, EFCommentArchiveRepository>();
        services.AddTransient<ICommentRenderingRepository, EFCommentRenderingRepository>();
        services.AddTransient<IAccountPurgeRepository, EFAccountPurgeRepository>();
        services.AddSingleton<IDbModelContribution, ChartCommentsModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical registers
    ///     its internal consumers explicitly through this hook — call it inside the host's
    ///     AddMassTransit block. CommunityTools once shipped with all 33 handlers unregistered and
    ///     every suite green, which is why this exists as a named hook rather than a scan.
    /// </summary>
    public static void AddChartCommentsConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<AccountPurgeConsumer>();
        configurator.AddConsumer<CommunityDeletionConsumer>();
        configurator.AddConsumer<CommentTranslationSaga>();
    }
}
