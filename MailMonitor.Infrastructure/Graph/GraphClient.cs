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

        public async Task<GraphConnectivityCheckResult> CheckConnectivityAsync(
            string userMail,
            string mailboxId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMail) || string.IsNullOrWhiteSpace(mailboxId))
            {
                return new GraphConnectivityCheckResult(
                    false,
                    userMail,
                    mailboxId,
                    "GraphHealth.InvalidTarget",
                    "User mail and mailbox id are required to validate Graph connectivity.");
            }

            try
            {
                var graphClient = await _graphAuthFactory.CreateClientAsync(cancellationToken);

                var response = await graphClient
                    .Users[userMail]
                    .MailFolders[mailboxId]
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Select = ["id"];
                    }, cancellationToken);

                if (response is null || string.IsNullOrWhiteSpace(response.Id))
                {
                    return new GraphConnectivityCheckResult(
                        false,
                        userMail,
                        mailboxId,
                        "GraphHealth.EmptyResponse",
                        "Graph connectivity check returned an empty mailbox response.");
                }

                return new GraphConnectivityCheckResult(
                    true,
                    userMail,
                    mailboxId,
                    string.Empty,
                    string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Graph connectivity check failed for user {UserMail} and mailbox {MailboxId}.",
                    userMail,
                    mailboxId);

                return new GraphConnectivityCheckResult(
                    false,
                    userMail,
                    mailboxId,
                    "GraphHealth.ConnectionFailed",
                    ex.Message);
            }
        }

        public async Task<IReadOnlyCollection<Message>> GetRecentMessagesAsync(
            string userMail,
            string mailboxId,
            int take = 5,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMail) || string.IsNullOrWhiteSpace(mailboxId))
            {
                return Array.Empty<Message>();
            }

            var normalizedTake = Math.Clamp(take, 1, 5);

            try
            {
                var graphClient = await _graphAuthFactory.CreateClientAsync(cancellationToken);

                var response = await graphClient
                    .Users[userMail]
                    .MailFolders[mailboxId]
                    .Messages
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Top = normalizedTake;
                        request.QueryParameters.Select =
                        [
                            "id",
                            "subject",
                            "receivedDateTime",
                            "hasAttachments",
                            "from"
                        ];
                        request.QueryParameters.Orderby = ["receivedDateTime desc"];
                    }, cancellationToken);

                return response?.Value?.ToList() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while reading recent messages for user {UserMail} and mailbox {MailboxId}.",
                    userMail,
                    mailboxId);

                return Array.Empty<Message>();
            }
        }

        public async Task<IReadOnlyCollection<MailboxLookupResult>> SearchMailboxesAsync(
            string userMail,
            string query,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMail))
            {
                return Array.Empty<MailboxLookupResult>();
            }

            try
            {
                var normalizedQuery = query?.Trim() ?? string.Empty;
                var allFolders = await GetMailboxCatalogAsync(userMail, cancellationToken);

                return allFolders
                    .Where(folder =>
                        string.IsNullOrWhiteSpace(normalizedQuery) ||
                        folder.DisplayName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        folder.Path.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                    .Take(50)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while searching mailboxes for user {UserMail}.", userMail);
                return Array.Empty<MailboxLookupResult>();
            }
        }

        public async Task<IReadOnlyCollection<MailboxLookupResult>> ResolveMailboxesAsync(
            string userMail,
            IReadOnlyCollection<string> mailboxIds,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMail) || mailboxIds is null || mailboxIds.Count == 0)
            {
                return Array.Empty<MailboxLookupResult>();
            }

            IReadOnlyCollection<MailboxLookupResult> allFolders = Array.Empty<MailboxLookupResult>();
            try
            {
                allFolders = await GetMailboxCatalogAsync(userMail, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to load mailbox catalog for user {UserMail}. Falling back to direct id resolution.",
                    userMail);
            }

            var byId = allFolders
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<MailboxLookupResult>();

            foreach (var rawId in mailboxIds)
            {
                var id = rawId?.Trim();
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    continue;
                }

                if (byId.TryGetValue(id, out var metadata))
                {
                    result.Add(metadata);
                    continue;
                }

                var fallback = await TryResolveMailboxByIdAsync(userMail, id, cancellationToken);
                if (fallback is not null)
                {
                    result.Add(fallback);
                }
            }

            return result;
        }

        private async Task<MailboxLookupResult?> TryResolveMailboxByIdAsync(
            string userMail,
            string mailboxId,
            CancellationToken cancellationToken)
        {
            try
            {
                var graphClient = await _graphAuthFactory.CreateClientAsync(cancellationToken);
                var folder = await graphClient
                    .Users[userMail]
                    .MailFolders[mailboxId]
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Select = ["id", "displayName"];
                    }, cancellationToken);

                var resolvedId = folder?.Id?.Trim();
                if (string.IsNullOrWhiteSpace(resolvedId))
                {
                    return null;
                }

                var displayName = NormalizeDisplayName(folder?.DisplayName, resolvedId);
                return new MailboxLookupResult(resolvedId, displayName, displayName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to resolve mailbox metadata directly for user {UserMail} and mailbox {MailboxId}.",
                    userMail,
                    mailboxId);

                return null;
            }
        }

        private static string NormalizeDisplayName(string? displayName, string fallbackId)
        {
            return string.IsNullOrWhiteSpace(displayName)
                ? fallbackId
                : displayName.Trim();
        }

        private async Task<IReadOnlyCollection<MailboxLookupResult>> GetMailboxCatalogAsync(
            string userMail,
            CancellationToken cancellationToken)
        {
            var graphClient = await _graphAuthFactory.CreateClientAsync(cancellationToken);
            var allFolders = new List<MailboxLookupResult>();
            var pendingFolders = new Queue<(string FolderId, string ParentPath)>();
            const int maxFolders = 500;

            var rootResponse = await graphClient
                .Users[userMail]
                .MailFolders
                .GetAsync(request =>
                {
                    request.QueryParameters.Top = 100;
                    request.QueryParameters.Select = ["id", "displayName", "childFolderCount"];
                }, cancellationToken);

            while (rootResponse is not null)
            {
                foreach (var folder in rootResponse.Value ?? [])
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var folderId = folder.Id?.Trim();
                    if (string.IsNullOrWhiteSpace(folderId))
                    {
                        continue;
                    }

                    var displayName = NormalizeDisplayName(folder.DisplayName, folderId);
                    var path = displayName;
                    allFolders.Add(new MailboxLookupResult(folderId, displayName, path));

                    if ((folder.ChildFolderCount ?? 0) > 0)
                    {
                        pendingFolders.Enqueue((folderId, path));
                    }

                    if (allFolders.Count >= maxFolders)
                    {
                        break;
                    }
                }

                if (allFolders.Count >= maxFolders || string.IsNullOrWhiteSpace(rootResponse.OdataNextLink))
                {
                    break;
                }

                rootResponse = await graphClient
                    .Users[userMail]
                    .MailFolders
                    .WithUrl(rootResponse.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken);
            }

            while (pendingFolders.Count > 0 && allFolders.Count < maxFolders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (parentId, parentPath) = pendingFolders.Dequeue();

                var childrenResponse = await graphClient
                    .Users[userMail]
                    .MailFolders[parentId]
                    .ChildFolders
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Top = 100;
                        request.QueryParameters.Select = ["id", "displayName", "childFolderCount"];
                    }, cancellationToken);

                while (childrenResponse is not null)
                {
                    foreach (var folder in childrenResponse.Value ?? [])
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var folderId = folder.Id?.Trim();
                        if (string.IsNullOrWhiteSpace(folderId))
                        {
                            continue;
                        }

                        var displayName = NormalizeDisplayName(folder.DisplayName, folderId);
                        var path = $"{parentPath}/{displayName}";
                        allFolders.Add(new MailboxLookupResult(folderId, displayName, path));

                        if ((folder.ChildFolderCount ?? 0) > 0)
                        {
                            pendingFolders.Enqueue((folderId, path));
                        }

                        if (allFolders.Count >= maxFolders)
                        {
                            break;
                        }
                    }

                    if (allFolders.Count >= maxFolders || string.IsNullOrWhiteSpace(childrenResponse.OdataNextLink))
                    {
                        break;
                    }

                    childrenResponse = await graphClient
                        .Users[userMail]
                        .MailFolders[parentId]
                        .ChildFolders
                        .WithUrl(childrenResponse.OdataNextLink)
                        .GetAsync(cancellationToken: cancellationToken);
                }
            }

            return allFolders
                .GroupBy(folder => folder.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
