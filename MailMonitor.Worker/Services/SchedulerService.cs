using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Worker.Jobs;
using Microsoft.Extensions.Options;
using Quartz;

namespace MailMonitor.Worker.Services
{
    public sealed class SchedulerService : IHostedService
    {
        private readonly ILogger<SchedulerService> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IConfigurationService _configurationService;
        private readonly WorkerSchedulingOptions _options;

        public SchedulerService(
            ILogger<SchedulerService> logger,
            ISchedulerFactory schedulerFactory,
            IConfigurationService configurationService,
            IOptions<WorkerSchedulingOptions> options)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _configurationService = configurationService;
            _options = options.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            await ScheduleMonitoringJobAsync(scheduler, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task ScheduleMonitoringJobAsync(IScheduler scheduler, CancellationToken cancellationToken)
        {
            var configuredTriggers = await _configurationService.GetTriggersAsync();

            var timeZone = ResolveTimeZone(_options.TimeZoneId);
            var quartzTriggers = new HashSet<ITrigger>();

            foreach (var configuredTrigger in configuredTriggers)
            {
                if (!CronExpression.IsValidExpression(configuredTrigger.CronExpression))
                {
                    _logger.LogWarning(
                        "Skipping invalid trigger {TriggerId} ({TriggerName}). Invalid cron expression: {CronExpression}",
                        configuredTrigger.Id,
                        configuredTrigger.Name,
                        configuredTrigger.CronExpression);
                    continue;
                }

                var quartzTrigger = TriggerBuilder.Create()
                    .WithIdentity($"mail-monitor-trigger-{configuredTrigger.Id}", "mail-monitor")
                    .ForJob(MailMonitorJob.JobKey)
                    .WithDescription(configuredTrigger.Name)
                    .WithCronSchedule(configuredTrigger.CronExpression, scheduleBuilder => scheduleBuilder
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing())
                    .Build();

                quartzTriggers.Add(quartzTrigger);
            }

            if (quartzTriggers.Count == 0)
            {
                var fallbackCron = CronExpression.IsValidExpression(_options.FallbackCronExpression)
                    ? _options.FallbackCronExpression
                    : "0 0/10 * ? * * *";

                _logger.LogWarning(
                    "No valid triggers found in configuration. Using fallback cron expression: {FallbackCronExpression}.",
                    fallbackCron);

                quartzTriggers.Add(TriggerBuilder.Create()
                    .WithIdentity("mail-monitor-fallback-trigger", "mail-monitor")
                    .ForJob(MailMonitorJob.JobKey)
                    .WithDescription("Fallback trigger")
                    .WithCronSchedule(fallbackCron, scheduleBuilder => scheduleBuilder
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing())
                    .Build());
            }

            var jobDetail = JobBuilder.Create<MailMonitorJob>()
                .WithIdentity(MailMonitorJob.JobKey)
                .WithDescription("Mail monitoring processing job")
                .Build();

            await scheduler.ScheduleJob(jobDetail, quartzTriggers, true, cancellationToken);

            _logger.LogInformation(
                "Scheduled mail monitor job with {TriggerCount} cron trigger(s) in timezone {TimeZoneId}.",
                quartzTriggers.Count,
                timeZone.Id);
        }

        private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return TimeZoneInfo.Utc;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
