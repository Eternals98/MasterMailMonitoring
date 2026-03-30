using Microsoft.Graph.Models;

namespace MailMonitor.Worker.Services
{
    public interface IMailboxReader
    {
        Task<IReadOnlyCollection<Message>> GetMessagesAsync(
            string userMail,
            string mailboxId,
            DateTimeOffset? startFromUtc,
            CancellationToken cancellationToken);

        Task<IReadOnlyCollection<FileAttachment>> GetFileAttachmentsAsync(
            string userMail,
            string messageId,
            CancellationToken cancellationToken);
    }
}
