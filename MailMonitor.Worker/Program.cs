using MailMonitor.Infrastructure;
using MailMonitor.Worker.Services;
using Quartz;

namespace MailMonitor.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddInfrastructure(builder.Configuration);

            builder.Services.Configure<WorkerSchedulingOptions>(
                builder.Configuration.GetSection(WorkerSchedulingOptions.SectionName));

            builder.Services.AddSingleton<Worker>();
            builder.Services.AddSingleton<IMailboxReader, GraphMailboxReader>();
            builder.Services.AddSingleton<IMessageFilterService, MessageFilterService>();
            builder.Services.AddSingleton<IAttachmentPersistenceService, AttachmentPersistenceService>();
            builder.Services.AddSingleton<IMessageTaggingService, MessageTaggingService>();
            builder.Services.AddSingleton<IProcessingLogService, ProcessingLogService>();

            builder.Services.AddQuartz();

            builder.Services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });

            builder.Services.AddHostedService<SchedulerService>();

            var host = builder.Build();
            host.Run();
        }
    }
}
