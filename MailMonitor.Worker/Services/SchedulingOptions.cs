namespace MailMonitor.Worker.Services
{
    public sealed class WorkerSchedulingOptions
    {
        public const string SectionName = "Scheduling";

        public string TimeZoneId { get; set; } = "UTC";
        public string FallbackCronExpression { get; set; } = "0 0/10 * ? * * *";
    }
}
