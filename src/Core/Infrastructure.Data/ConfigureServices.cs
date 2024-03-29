﻿using System.Reflection;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Infrastructure.Data.Context;
using LayeredTemplate.Infrastructure.Data.Interceptors;
using LayeredTemplate.Infrastructure.Data.Services;
using LayeredTemplate.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Data;

public static class ConfigureServices
{
    public static void RegisterDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextPool<ApplicationDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, x =>
                {
                    x.MigrationsHistoryTable("__ef_backend_migrations");

                    x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .UseSnakeCaseNamingConvention();

            options.AddInterceptors(new BaseEntitySaveChangesInterceptor());
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbConnection>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddDataProtection()
            .PersistKeysToDbContext<ApplicationDbContext>();

        services.AddStartupTask<RunMigrationsTask<ApplicationDbContext>>();
    }
}