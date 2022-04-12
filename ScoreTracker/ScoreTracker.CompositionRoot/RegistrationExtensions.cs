using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CompositionRoot;

public static class RegistrationExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection builder)
    {
        return builder.AddMediatR(typeof(RecordAttemptHandler));
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection builder)
    {
        foreach (var implementationType in typeof(EFChartRepository).Assembly.GetTypes()
                )
        foreach (var interfaceType in implementationType.GetInterfaces()
                     .Where(i => i.Assembly == typeof(IChartRepository).Assembly))
            builder.AddTransient(interfaceType, implementationType);
        return builder.AddDbContext<ChartAttemptDbContext>(o => { o.UseInMemoryDatabase("ChartAttempts"); });
    }
}