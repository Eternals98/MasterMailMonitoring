using Quartz;

namespace MailMonitor.Worker.Jobs
{
    [DisallowConcurrentExecution]
    public sealed class MailMonitorJob : IJob
    {
        public static readonly JobKey JobKey = new("mail-monitor-job", "mail-monitor");

        private readonly Worker _worker;
        private readonly ILogger<MailMonitorJob> _logger;

        public MailMonitorJob(Worker worker, ILogger<MailMonitorJob> logger)
        {
            _worker = worker;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var startedAt = DateTime.UtcNow;

            _logger.LogInformation("Mail monitor cycle started at {StartedAt}.", startedAt);

            try
            {
                await _worker.RunCycleAsync(context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during mail monitor cycle.");
                throw;
            }
            finally
            {
                var elapsed = DateTime.UtcNow - startedAt;
                _logger.LogInformation(
                    "Mail monitor cycle finished. DurationMs: {DurationMs}.",
                    elapsed.TotalMilliseconds);
            }
        }
    }
}
