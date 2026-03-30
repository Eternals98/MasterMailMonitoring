using MailMonitor.Application.Abstractions.Graph;
using MailMonitor.Domain.Abstractions;

namespace MailMonitor.Worker.Services
{
    public sealed class MessageTaggingService : IMessageTaggingService
    {
        private readonly IGraphClient _graphClient;

        public MessageTaggingService(IGraphClient graphClient)
        {
            _graphClient = graphClient;
        }

        public Task<Result> TagMessageAsync(
            string userMail,
            string messageId,
            string processingTag,
            CancellationToken cancellationToken)
        {
            return _graphClient.TagMessageAsync(userMail, messageId, processingTag, cancellationToken);
        }
    }
}
