using System.Reflection;
using FluentValidation;
using LayeredTemplate.App.Application.Common.Behaviors;
using LayeredTemplate.App.Application.Features.Info.Requests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.App.Application;

public static class ConfigureServices
{
    public static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ValidatorOptions.Global.LanguageManager.Enabled = false;

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);
        services.AddMediator(opts =>
        {
            opts.Assemblies = [typeof(InfoGetRequest)];
            opts.PipelineBehaviors = [typeof(ValidationBehaviour<,>)];
        });
    }
}