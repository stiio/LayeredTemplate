using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Extensions;

internal static class AwsServicesExtensions
{
    public static void ConfigureAwsServices(this IServiceCollection services, IConfiguration configuration)
    {
        var region = configuration["AWS_REGION"];

        if (string.IsNullOrEmpty(region))
        {
            return;
        }

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