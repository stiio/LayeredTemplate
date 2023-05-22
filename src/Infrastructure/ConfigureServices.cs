using System.Data;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.Data;
using LayeredTemplate.Infrastructure.Data.Context;
using LayeredTemplate.Infrastructure.Extensions;
using LayeredTemplate.Infrastructure.Mocks.Services;
using LayeredTemplate.Infrastructure.Services;
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

        services.AddMassTransit(opts =>
        {
            opts.SetKebabCaseEndpointNameFormatter();
            opts.AddConsumers(typeof(Application.ConfigureServices).Assembly);

            if (env.IsDevelopment())
            {
                opts.UsingInMemory((ctx, cfg) =>
                {
                    cfg.UseInMemoryOutbox();
                    cfg.ConfigureEndpoints(ctx);
                });
            }
            else
            {
                opts.UsingAmazonSqs((ctx, cfg) =>
                {
                    cfg.UseInMemoryOutbox();
                    cfg.Host(configuration["AWS_REGION"], _ =>
                    {
                        _.Scope($"{env.EnvironmentName.ToLower()}");
                    });

                    cfg.ConfigureEndpoints(ctx, new DefaultEndpointNameFormatter($"{env.EnvironmentName.ToLower()}-", true));
                });
            }
        });
    }
}