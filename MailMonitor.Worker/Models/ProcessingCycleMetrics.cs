namespace MailMonitor.Worker.Models
{
    public sealed class ProcessingCycleMetrics
    {
        public int Read { get; private set; }
        public int Processed { get; private set; }
        public int Ignored { get; private set; }
        public int Failed { get; private set; }

        public void IncrementRead() => Read++;
        public void IncrementProcessed() => Processed++;
        public void IncrementIgnored() => Ignored++;
        public void IncrementFailed() => Failed++;
    }
}
