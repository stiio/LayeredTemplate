using LayeredTemplate.Infrastructure.Scheduler.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LayeredTemplate.Infrastructure.Scheduler.Services;

internal class SchedulerExampleJobService
{
    private readonly ISchedulerFactory schedulerFactory;
    private readonly ILogger<SchedulerExampleJobService> logger;

    public SchedulerExampleJobService(
        ISchedulerFactory schedulerFactory,
        ILogger<SchedulerExampleJobService> logger)
    {
        this.schedulerFactory = schedulerFactory;
        this.logger = logger;
    }

    public async Task ScheduleExampleJob(string key, DateTime targetDate)
    {
        var scheduler = await this.schedulerFactory.GetScheduler();

        var triggerKey = new TriggerKey(key, ExampleJob.Key.Group);

        if (targetDate < DateTime.UtcNow)
        {
            await scheduler.UnscheduleJob(triggerKey);
            return;
        }

        await scheduler.UnscheduleJob(triggerKey);

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(ExampleJob.Key)
            .UsingJobData("someData", "D9B4D244-FC05-43D6-B072-5691FE01F85C")
            .StartAt(targetDate)
            .Build();

        var result = await scheduler.ScheduleJob(trigger);

        this.logger.LogInformation("Create trigger for example job. Target Date: {targetDate}", result);
    }
}