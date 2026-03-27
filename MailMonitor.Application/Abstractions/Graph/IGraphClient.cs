using MailMonitor.Domain.Abstractions;
using Microsoft.Graph.Models;

namespace MailMonitor.Application.Abstractions.Graph
{
    public interface IGraphClient
    {
        Task<IReadOnlyCollection<Message>> GetMessagesAsync(
            string userMail,
            string mailboxId,
            DateTimeOffset? startFromUtc,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<FileAttachment>> GetFileAttachmentsAsync(
            string userMail,
            string messageId,
            CancellationToken cancellationToken = default);

        Task<Result> TagMessageAsync(
            string userMail,
            string messageId,
            string categoryTag,
            CancellationToken cancellationToken = default);
    }
}
