using Azure.Identity;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Domain.Entities.Graph;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace MailMonitor.Infrastructure.Graph
{
    internal sealed class GraphAuthFactory
    {
        private readonly IConfigurationService _configurationService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GraphAuthFactory> _logger;

        public GraphAuthFactory(
            IConfigurationService configurationService,
            IConfiguration configuration,
            ILogger<GraphAuthFactory> logger)
        {
            _configurationService = configurationService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GraphServiceClient> CreateClientAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _configurationService.GetGraphSettingsAsync();
            var effectiveSettings = MergeWithEnvironmentSettings(settings);

            var validationResult = effectiveSettings.Validate();
            if (validationResult.IsFailure)
            {
                throw new InvalidOperationException($"Invalid graph settings: {validationResult.Error.Code} - {validationResult.Error.Name}");
            }

            var scopes = effectiveSettings.GetScopes();
            if (!scopes.Any())
            {
                scopes = ["https://graph.microsoft.com/.default"];
            }

            _logger.LogDebug("Creating GraphServiceClient with {ScopeCount} scopes.", scopes.Count);

            var credential = new ClientSecretCredential(
                effectiveSettings.TenantId,
                effectiveSettings.ClientId,
                effectiveSettings.ClientSecret);

            return new GraphServiceClient(credential, scopes);
        }

        private GraphSetting MergeWithEnvironmentSettings(GraphSetting? persistedSettings)
        {
            var settings = persistedSettings ?? new GraphSetting();

            var merged = new GraphSetting
            {
                Instance = ResolveValue(_configuration["Graph:Instance"], settings.Instance, "https://login.microsoftonline.com/"),
                ClientId = ResolveValue(_configuration["Graph:ClientId"], settings.ClientId),
                TenantId = ResolveValue(_configuration["Graph:TenantId"], settings.TenantId),
                ClientSecret = ResolveValue(_configuration["Graph:ClientSecret"], settings.ClientSecret),
                GraphUserScopesJson = settings.GraphUserScopesJson
            };

            var configuredScopes = ReadScopesFromConfiguration();
            if (configuredScopes is { Length: > 0 })
            {
                merged.SetScopes(configuredScopes);
            }
            else if (!merged.GetScopes().Any())
            {
                merged.SetScopes(["https://graph.microsoft.com/.default"]);
            }

            return merged;
        }

        private static string ResolveValue(string? primary, string? secondary, string fallback = "")
        {
            if (!string.IsNullOrWhiteSpace(primary))
            {
                return primary.Trim();
            }

            if (!string.IsNullOrWhiteSpace(secondary))
            {
                return secondary.Trim();
            }

            return fallback;
        }

        private string[] ReadScopesFromConfiguration()
        {
            return _configuration
                .GetSection("Graph:Scopes")
                .GetChildren()
                .Select(section => section.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
