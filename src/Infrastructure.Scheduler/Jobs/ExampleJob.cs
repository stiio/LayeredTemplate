using Microsoft.Extensions.Logging;
using Quartz;

namespace LayeredTemplate.Infrastructure.Scheduler.Jobs;

internal class ExampleJob : IJob
{
    public static readonly JobKey Key = new("example_job_name", "example_job_group");

    private readonly ILogger<ExampleJob> logger;

    public ExampleJob(ILogger<ExampleJob> logger)
    {
        this.logger = logger;
    }

    public string? SomeData { private get; set; }

    public Task Execute(IJobExecutionContext context)
    {
        this.logger.LogInformation($"Example job processing. Some data: {this.SomeData}");
        return Task.CompletedTask;
    }
}