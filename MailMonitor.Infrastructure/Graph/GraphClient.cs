using MailMonitor.Application.Abstractions.Graph;
using MailMonitor.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;

namespace MailMonitor.Infrastructure.Graph
{
    internal sealed class GraphClient : IGraphClient
    {
        private readonly GraphAuthFactory _graphAuthFactory;
        private readonly ILogger<GraphClient> _logger;

        public GraphClient(
            GraphAuthFactory graphAuthFactory,
            ILogger<GraphClient> logger)
        {
            _graphAuthFactory = graphAuthFactory;
            _logger = logger;
        }

        public async Task<IReadOnlyCollection<Message>> GetMessagesAsync(
            string userMail,
            string mailboxId,
            DateTimeOffset? startFromUtc,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMail) || string.IsNullOrWhiteSpace(mailboxId))
            {
                return Array.Empty<Message>();
            }

            try
            {
                var graphClient = await _graphAuthFactory.CreateClientAsync(cancellationToken);
                var messages = new List<Message>();

                var response = await graphClient
                    .Users[userMail]
                    .MailFolders[mailboxId]
                    .Messages
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Top = 50;
                        request.QueryParameters.Select =
                        [
                            "id",
                            "subject",
                            "receivedDateTime",
                            "hasAttachments",
                            "categories"
                        ];

                        request.QueryParameters.Orderby = ["receivedDateTime desc"];

                        if (startFromUtc.HasValue)
                        {
                            request.QueryParameters.Filter = $"receivedDateTime ge {startFromUtc.Value.UtcDateTime:O}";
                        }
                    }, cancellationToken);

                while (response is not null)
                {
                    if (response.Value is not null)
                    {
                        messages.AddRange(response.Value);
                    }

                    if (string.IsNullOrWhiteSpace(response.OdataNextLink))
                    {
                        break;
                    }

                    response = await graphClient
                        .Users[userMail]
                        .MailFolders[mailboxId]
                        .Messages
                        .WithUrl(response.OdataNextLink)
                        .GetAsync(cancellationToken: cancellationToken);
                }

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading messages for user {UserMail} and mailbox {MailboxId}.", userMail, mailboxId);
                return Array.Empty<Message>();
            }
        }

        public async Task<IReadOnlyCollection<FileAttachment>> GetFileAttachmentsAsync(
            string userMail,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMail) || string.IsNullOrWhiteSpace(messageId))
            {
                return Array.Empty<FileAttachment>();
            }

            try
            {
                var graphClient = await _graphAuthFactory.CreateClientAsync(cancellationToken);
                var attachments = new List<FileAttachment>();

                var response = await graphClient
                    .Users[userMail]
                    .Messages[messageId]
                    .Attachments
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Top = 50;
                        request.QueryParameters.Select =
                        [
                            "id",
                            "name",
                            "contentType",
                            "size",
                            "contentBytes"
                        ];
                    }, cancellationToken);

                while (response is not null)
                {
                    if (response.Value is not null)
                    {
                        attachments.AddRange(response.Value.OfType<FileAttachment>());
                    }

                    if (string.IsNullOrWhiteSpace(response.OdataNextLink))
                    {
                        break;
                    }

                    response = await graphClient
                        .Users[userMail]
                        .Messages[messageId]
                        .Attachments
                        .WithUrl(response.OdataNextLink)
                        .GetAsync(cancellationToken: cancellationToken);
                }

                return attachments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading attachments for message {MessageId} and user {UserMail}.", messageId, userMail);
                return Array.Empty<FileAttachment>();
            }
        }

        public async Task<Result> TagMessageAsync(
            string userMail,
            string messageId,
            string categoryTag,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMail) || string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(categoryTag))
            {
                return Result.Failure(Error.EmptyValue);
            }

            try
            {
                var graphClient = await _graphAuthFactory.CreateClientAsync(cancellationToken);

                var currentMessage = await graphClient
                    .Users[userMail]
                    .Messages[messageId]
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Select = ["id", "categories"];
                    }, cancellationToken);

                var categories = (currentMessage?.Categories ?? new List<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!categories.Contains(categoryTag, StringComparer.OrdinalIgnoreCase))
                {
                    categories.Add(categoryTag.Trim());
                }

                await graphClient
                    .Users[userMail]
                    .Messages[messageId]
                    .PatchAsync(new Message { Categories = categories }, cancellationToken: cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while tagging message {MessageId} for user {UserMail}.", messageId, userMail);
                return Result.Failure(Error.Failure);
            }
        }
    }
}
