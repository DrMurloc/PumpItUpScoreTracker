﻿using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.CompositionRoot;

public static class RegistrationExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection builder)
    {
        return builder.AddMediatR(typeof(UpdateBestAttemptHandler))
            .AddTransient<IUserAccessService, UserAccessService>();
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection builder, SqlConfiguration configuration)
    {
        foreach (var implementationType in typeof(EFChartRepository).Assembly.GetTypes()
                )
        foreach (var interfaceType in implementationType.GetInterfaces()
                     .Where(i => i.Assembly == typeof(IChartRepository).Assembly))
            builder.AddTransient(interfaceType, implementationType);
        builder.Configure<SqlConfiguration>(o => { o.ConnectionString = configuration.ConnectionString; });
        return builder.AddDbContext<ChartAttemptDbContext>(o => { o.UseSqlServer(configuration.ConnectionString); });
    }
}