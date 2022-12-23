using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.SecurityToken.Model;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.AuthorizationHandlers;
using LayeredTemplate.Infrastructure.Data;
using LayeredTemplate.Infrastructure.Services;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure;

public static class ConfigureServices
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterDbContext(configuration[ConnectionStrings.DefaultConnection]!);

        services.AddScoped<IClaimsTransformation, AppClaimTransformation>();

        services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
        services.AddScoped<IAuthorizationHandler, TodoListAuthorizationHandler>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserPoolService, UserPoolService>();

        services.ConfigureAwsServices(configuration);
    }

    private static void ConfigureAwsServices(this IServiceCollection services, IConfiguration configuration)
    {
        var awsOptions = configuration.GetAWSOptions();
        awsOptions.Region = RegionEndpoint.GetBySystemName(configuration["AWS_REGION"]);
        awsOptions.Credentials = new Credentials()
        {
            AccessKeyId = configuration["AWS_ACCESS_KEY_ID"],
            SecretAccessKey = configuration["AWS_SECRET_ACCESS_KEY"],
        };
        services.AddDefaultAWSOptions(awsOptions);

        services.AddAWSService<IAmazonCognitoIdentityProvider>();
    }
}