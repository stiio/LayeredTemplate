using LayeredTemplate.Infrastructure.Scheduler.Jobs;
using LayeredTemplate.Shared.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace LayeredTemplate.Infrastructure.Scheduler;

public static class ConfigureServices
{
    public static void ConfigureScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddQuartz(options =>
        {
            options.SchedulerName = "Scheduler-Core";
            options.SchedulerId = "AUTO";
            options.UseMicrosoftDependencyInjectionJobFactory();
            options.UseDefaultThreadPool(1);

            options.UsePersistentStore(x =>
            {
                x.UseClustering();
                x.UsePostgres(configuration[ConnectionStrings.DefaultConnection]!);
                x.UseJsonSerializer();
                x.UseProperties = true;
            });

            options.AddJob<ExampleJob>(
                ExampleJob.Key,
                jobConfiguration =>
                {
                    jobConfiguration.RequestRecovery(false);
                    jobConfiguration.StoreDurably();
                });
        });

        services.AddQuartzHostedService(options =>
        {
            options.AwaitApplicationStarted = true;
            options.WaitForJobsToComplete = true;
        });
    }
}