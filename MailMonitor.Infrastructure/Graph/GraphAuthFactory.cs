using Azure.Identity;
using MailMonitor.Application.Abstractions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace MailMonitor.Infrastructure.Graph
{
    internal sealed class GraphAuthFactory
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<GraphAuthFactory> _logger;

        public GraphAuthFactory(
            IConfigurationService configurationService,
            ILogger<GraphAuthFactory> logger)
        {
            _configurationService = configurationService;
            _logger = logger;
        }

        public async Task<GraphServiceClient> CreateClientAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _configurationService.GetGraphSettingsAsync();
            if (settings is null)
            {
                throw new InvalidOperationException("Graph settings are not configured.");
            }

            var validationResult = settings.Validate();
            if (validationResult.IsFailure)
            {
                throw new InvalidOperationException($"Invalid graph settings: {validationResult.Error.Code} - {validationResult.Error.Name}");
            }

            var scopes = settings.GetScopes();
            if (!scopes.Any())
            {
                scopes = ["https://graph.microsoft.com/.default"];
            }

            _logger.LogDebug("Creating GraphServiceClient with {ScopeCount} scopes.", scopes.Count);

            var credential = new ClientSecretCredential(
                settings.TenantId,
                settings.ClientId,
                settings.ClientSecret);

            return new GraphServiceClient(credential, scopes);
        }
    }
}
