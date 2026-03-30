using MailMonitor.Application.Abstractions.Graph;
using Microsoft.Graph.Models;

namespace MailMonitor.Worker.Services
{
    public sealed class GraphMailboxReader : IMailboxReader
    {
        private readonly IGraphClient _graphClient;

        public GraphMailboxReader(IGraphClient graphClient)
        {
            _graphClient = graphClient;
        }

        public Task<IReadOnlyCollection<Message>> GetMessagesAsync(
            string userMail,
            string mailboxId,
            DateTimeOffset? startFromUtc,
            CancellationToken cancellationToken)
        {
            return _graphClient.GetMessagesAsync(userMail, mailboxId, startFromUtc, cancellationToken);
        }

        public Task<IReadOnlyCollection<FileAttachment>> GetFileAttachmentsAsync(
            string userMail,
            string messageId,
            CancellationToken cancellationToken)
        {
            return _graphClient.GetFileAttachmentsAsync(userMail, messageId, cancellationToken);
        }
    }
}
