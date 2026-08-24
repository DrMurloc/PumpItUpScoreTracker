using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Translations.Application;
using ScoreTracker.Translations.Domain;
using ScoreTracker.Translations.Infrastructure;

namespace ScoreTracker.Translations.Wiring;

/// <summary>
///     Wires the Translations vertical — real since the comments feature's Slice 4, when this
///     stopped being an assembly-marker stub. MediatR handlers are found by the host's assembly
///     scan; bus consumers are NOT — see <see cref="AddTranslationsConsumers" />.
///     <para>
///         Nothing here arms metered spend. The batch client the pipeline rides reports itself
///         unconfigured without a <c>ClaudeApi:ApiKey</c>, and the submit step parks on that —
///         configuration is what turns money on, never registration.
///     </para>
/// </summary>
public static class TranslationsRegistrationExtensions
{
    public static IServiceCollection AddTranslations(this IServiceCollection services)
    {
        services.AddTransient<ITranslationRequestRepository, EFTranslationRequestRepository>();
        services.AddTransient<ITranslationBatchRepository, EFTranslationBatchRepository>();
        services.AddSingleton<IDbModelContribution, TranslationsModelContribution>();
        return services;
    }

    /// <summary>
    ///     MassTransit's AddConsumers assembly scan skips internal types, so the vertical
    ///     registers its internal consumers explicitly through this hook — call it inside the
    ///     host's AddMassTransit block. CommunityTools once shipped with all 33 handlers
    ///     unregistered and every suite green, which is why this exists as a named hook rather
    ///     than a scan.
    /// </summary>
    public static void AddTranslationsConsumers(this IRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<TranslationPipelineSaga>();
    }
}
