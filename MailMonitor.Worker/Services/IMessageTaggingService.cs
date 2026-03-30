using MailMonitor.Domain.Abstractions;

namespace MailMonitor.Worker.Services
{
    public interface IMessageTaggingService
    {
        Task<Result> TagMessageAsync(
            string userMail,
            string messageId,
            string processingTag,
            CancellationToken cancellationToken);
    }
}
