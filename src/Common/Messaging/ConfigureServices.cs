using System.Net.Mime;
using System.Reflection;
using Humanizer;
using LayeredTemplate.Messaging.BusFilters;
using LayeredTemplate.Messaging.Formatters;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Messaging;

public static class ConfigureServices
{
    public static void AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly? assembly = null,
        Action<IBusRegistrationConfigurator>? busConfiguration = null,
        Action<IBusFactoryConfigurator>? busFactoryConfiguration = null)
    {
        services.AddMassTransit(opts =>
        {
            if (assembly is not null)
            {
                opts.AddConsumers(assembly);
            }

            // Uncomment for sqs
            /*opts.AddConfigureEndpointsCallback((name, configurator) =>
            {
                if (configurator is IAmazonSqsReceiveEndpointConfigurator sqsReceiveEndpointConfigurator)
                {
                    sqsReceiveEndpointConfigurator.WaitTimeSeconds = 20;
                }
            });*/

            busConfiguration?.Invoke(opts);

            opts.UsingInMemory((ctx, cfg) =>
            {
                cfg.UseMessageScope(ctx);
                cfg.UseInMemoryOutbox(ctx);

                cfg.UseConsumeFilter(typeof(LoggerScopeFilter<>), ctx);
                busFactoryConfiguration?.Invoke(cfg);

                cfg.MessageTopology.SetEntityNameFormatter(new KebabCaseEntityNameFormatter(new CustomAmazonSqsMessageNameFormatter(), configuration["ASPNETCORE_ENVIRONMENT"].Kebaberize()));
                cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(configuration["ASPNETCORE_ENVIRONMENT"].Kebaberize(), false));
            });

            // Uncomment for sqs
            /*opts.UsingAmazonSqs((ctx, cfg) =>
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
                busFactoryConfiguration?.Invoke(cfg);

                cfg.MessageTopology.SetEntityNameFormatter(new KebabCaseEntityNameFormatter(new CustomAmazonSqsMessageNameFormatter(), env.EnvironmentName.Kebaberize()));
                cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(env.EnvironmentName.Kebaberize(), false));
            });*/
        });

        services.AddOptions<MassTransitHostOptions>()
            .Configure(options =>
            {
                options.WaitUntilStarted = true;
                options.StartTimeout = TimeSpan.FromMinutes(1);
                options.StopTimeout = TimeSpan.FromMinutes(1);
            });
    }
}