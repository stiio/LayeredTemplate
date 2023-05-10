using System.Reflection;
using FluentValidation;
using LayeredTemplate.Application.Common.Behaviors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Application;

public static class ConfigureServices
{
    public static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ValidatorOptions.Global.LanguageManager.Enabled = false;

        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);
        services.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            opts.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });
    }
}