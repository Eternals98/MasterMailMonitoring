using MailMonitor.Domain.Abstractions;
using Microsoft.Graph.Models;

namespace MailMonitor.Application.Abstractions.Graph
{
    public sealed record GraphConnectivityCheckResult(
        bool IsSuccess,
        string UserMail,
        string MailboxId,
        string ErrorCode,
        string ErrorMessage);

    public sealed record MailboxLookupResult(
        string Id,
        string DisplayName,
        string Path);

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

        Task<GraphConnectivityCheckResult> CheckConnectivityAsync(
            string userMail,
            string mailboxId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<Message>> GetRecentMessagesAsync(
            string userMail,
            string mailboxId,
            int take = 5,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<MailboxLookupResult>> SearchMailboxesAsync(
            string userMail,
            string query,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<MailboxLookupResult>> ResolveMailboxesAsync(
            string userMail,
            IReadOnlyCollection<string> mailboxIds,
            CancellationToken cancellationToken = default);
    }
}
