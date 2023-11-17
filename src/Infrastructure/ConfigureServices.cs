using Humanizer;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.BusFilters;
using LayeredTemplate.Infrastructure.Data;
using LayeredTemplate.Infrastructure.Extensions;
using LayeredTemplate.Infrastructure.Mocks.Services;
using LayeredTemplate.Infrastructure.Services;
using LayeredTemplate.Messaging.Formatters;
using LayeredTemplate.Shared.Constants;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayeredTemplate.Infrastructure;

public static class ConfigureServices
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services.RegisterDbContext(configuration[ConnectionStrings.DefaultConnection]!);

        services.ConfigureAuthentication(configuration);
        services.ConfigureAuthorization();
        services.ConfigureAwsServices(configuration);

        services.AddSingleton<ILockProvider, PostgresLockProvider>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserManager, UserManager>();

        if (configuration.GetValue<bool>("MOCK_USER_POOL"))
        {
            services.AddScoped<IUserPoolService, UserPoolServiceMock>();
        }
        else
        {
            services.AddScoped<IUserPoolService, UserPoolService>();
        }

        if (configuration.GetValue<bool>("MOCK_EMAIL_SENDER"))
        {
            services.AddScoped<IEmailSender, EmailSenderMock>();
        }
        else
        {
            services.AddScoped<IEmailSender, EmailSender>();
        }

        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromMinutes(1);
        });

        services.AddMassTransit(opts =>
        {
            opts.AddConsumers(typeof(Application.ConfigureServices).Assembly);
            opts.AddConfigureEndpointsCallback((name, configurator) =>
            {
                if (configurator is IAmazonSqsReceiveEndpointConfigurator sqsReceiveEndpointConfigurator)
                {
                    sqsReceiveEndpointConfigurator.WaitTimeSeconds = 20;
                }
            });

            if (env.IsDevelopment())
            {
                opts.UsingInMemory((ctx, cfg) =>
                {
                    cfg.UseMessageScope(ctx);
                    cfg.UseInMemoryOutbox(ctx);

                    cfg.UseConsumeFilter(typeof(LoggerScopeFilter<>), ctx);

                    cfg.MessageTopology.SetEntityNameFormatter(new KebabCaseEntityNameFormatter(new CustomAmazonSqsMessageNameFormatter(), env.EnvironmentName.Kebaberize()));
                    cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(env.EnvironmentName.Kebaberize(), false));
                });
            }
            else
            {
                opts.UsingAmazonSqs((ctx, cfg) =>
                {
                    cfg.WaitTimeSeconds = 20;

                    cfg.UseMessageScope(ctx);
                    cfg.UseInMemoryOutbox(ctx);

                    cfg.Host(configuration["AWS_REGION"], _ =>
                    {
                        _.AccessKey(configuration["AWS_ACCESS_KEY_ID"]);
                        _.SecretKey(configuration["AWS_SECRET_ACCESS_KEY"]);
                        _.Scope($"{env.EnvironmentName.Kebaberize()}", true);
                    });

                    cfg.UseConsumeFilter(typeof(LoggerScopeFilter<>), ctx);

                    cfg.MessageTopology.SetEntityNameFormatter(new KebabCaseEntityNameFormatter(new CustomAmazonSqsMessageNameFormatter(), env.EnvironmentName.Kebaberize()));
                    cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(env.EnvironmentName.Kebaberize(), false));
                });
            }
        });

        services.AddOptions<MassTransitHostOptions>()
            .Configure(options =>
            {
                options.WaitUntilStarted = true;
                options.StartTimeout = TimeSpan.FromMinutes(1);
                options.StopTimeout = TimeSpan.FromMinutes(1);
            });

        services.AddHealthChecks()
            .AddNpgSql(configuration[ConnectionStrings.DefaultConnection]!);
    }
}