using System.Text.Json;
using MailMonitor.Domain.Abstractions;

namespace MailMonitor.Domain.Entities.Graph
{
    public sealed class GraphSetting
    {
        public int Id { get; set; }
        public string Instance { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string GraphUserScopesJson { get; set; } = "[]";

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(Instance))
            {
                return Result.Failure(DomainErrors.GraphSetting.InstanceRequired);
            }

            if (string.IsNullOrWhiteSpace(ClientId))
            {
                return Result.Failure(DomainErrors.GraphSetting.ClientIdRequired);
            }

            if (string.IsNullOrWhiteSpace(TenantId))
            {
                return Result.Failure(DomainErrors.GraphSetting.TenantIdRequired);
            }

            if (string.IsNullOrWhiteSpace(ClientSecret))
            {
                return Result.Failure(DomainErrors.GraphSetting.ClientSecretRequired);
            }

            try
            {
                GetScopes();
            }
            catch (JsonException)
            {
                return Result.Failure(DomainErrors.GraphSetting.InvalidScopesJson);
            }

            return Result.Success();
        }

        public IReadOnlyCollection<string> GetScopes()
        {
            var scopes = JsonSerializer.Deserialize<List<string>>(GraphUserScopesJson) ?? [];
            return scopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void SetScopes(IEnumerable<string> scopes)
        {
            var normalizedScopes = scopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            GraphUserScopesJson = JsonSerializer.Serialize(normalizedScopes);
        }
    }
}
