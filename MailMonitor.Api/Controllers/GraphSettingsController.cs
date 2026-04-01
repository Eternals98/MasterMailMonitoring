using System.Text.Json;
using MailMonitor.Api.Contracts.GraphSettings;
using MailMonitor.Api.Contracts.Health;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Application.Abstractions.Graph;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Graph;
using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/graph-settings")]
public sealed class GraphSettingsController : ControllerBase
{
    private readonly IConfigurationService _configurationService;
    private readonly IGraphClient _graphClient;

    public GraphSettingsController(
        IConfigurationService configurationService,
        IGraphClient graphClient)
    {
        _configurationService = configurationService;
        _graphClient = graphClient;
    }

    /// <summary>
    /// Gets Microsoft Graph settings with masked secret.
    /// </summary>
    /// <response code="200">Graph settings found. Example: {"instance":"https://login.microsoftonline.com/","clientSecretMasked":"******cret"}</response>
    /// <response code="404">Graph settings not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(GraphSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GraphSettingsResponse>> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var graphSettings = await _configurationService.GetGraphSettingsAsync();
        if (graphSettings is null)
        {
            return NotFound();
        }

        var response = new GraphSettingsResponse(
            graphSettings.Instance,
            graphSettings.ClientId,
            graphSettings.TenantId,
            MaskSecret(graphSettings.ClientSecret),
            graphSettings.GraphUserScopesJson,
            graphSettings.LastVerificationAtUtc,
            graphSettings.LastVerificationSucceeded,
            graphSettings.LastVerificationErrorCode,
            graphSettings.LastVerificationErrorMessage);

        return Ok(response);
    }

    /// <summary>
    /// Updates Microsoft Graph settings.
    /// </summary>
    /// <response code="204">Settings updated.</response>
    /// <response code="400">Invalid payload or invalid scopes JSON. Example: {"errors":{"graphUserScopesJson":["GraphUserScopesJson must be a valid JSON array of strings."]}}</response>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateGraphSettingsRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!IsValidScopesJson(request.GraphUserScopesJson))
        {
            ModelState.AddModelError(
                nameof(request.GraphUserScopesJson),
                "GraphUserScopesJson must be a valid JSON array of strings.");

            return ValidationProblem(ModelState);
        }

        var graphSettings = new GraphSetting
        {
            Instance = request.Instance,
            ClientId = request.ClientId,
            TenantId = request.TenantId,
            ClientSecret = request.ClientSecret,
            GraphUserScopesJson = request.GraphUserScopesJson
        };

        var validationResult = graphSettings.Validate();
        if (validationResult.IsFailure)
        {
            ModelState.AddModelError(nameof(request), validationResult.Error.Name);
            return ValidationProblem(ModelState);
        }

        await _configurationService.UpdateGraphSettingsAsync(graphSettings);

        return NoContent();
    }

    /// <summary>
    /// Verifies Microsoft Graph connectivity and stores the last verification result.
    /// </summary>
    /// <response code="200">Connectivity check succeeded.</response>
    /// <response code="503">Connectivity check failed or no mailbox target is configured.</response>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(GraphConnectivityHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GraphConnectivityHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GraphConnectivityHealthResponse>> VerifyAsync(
        [FromBody] VerifyGraphConnectionRequest? request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = await ResolveTargetAsync(request?.UserMail, request?.MailboxId);
        if (target is null)
        {
            var targetNotConfigured = new GraphConnectivityHealthResponse(
                DateTime.UtcNow,
                false,
                string.Empty,
                string.Empty,
                "GraphHealth.TargetNotConfigured",
                "No mailbox target is available for Graph connectivity validation.");

            await PersistVerificationResultAsync(targetNotConfigured);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, targetNotConfigured);
        }

        var result = await _graphClient.CheckConnectivityAsync(
            target.Value.UserMail,
            target.Value.MailboxId,
            cancellationToken);

        var response = new GraphConnectivityHealthResponse(
            DateTime.UtcNow,
            result.IsSuccess,
            result.UserMail,
            result.MailboxId,
            result.ErrorCode,
            result.ErrorMessage);

        await PersistVerificationResultAsync(response);

        return response.Healthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private static bool IsValidScopesJson(string scopesJson)
    {
        if (string.IsNullOrWhiteSpace(scopesJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(scopesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return string.Empty;
        }

        if (secret.Length <= 4)
        {
            return new string('*', secret.Length);
        }

        return $"{new string('*', secret.Length - 4)}{secret[^4..]}";
    }

    private async Task PersistVerificationResultAsync(GraphConnectivityHealthResponse response)
    {
        var graphSettings = await _configurationService.GetGraphSettingsAsync();
        if (graphSettings is null)
        {
            return;
        }

        graphSettings.SetVerificationResult(
            response.Healthy,
            response.ErrorCode,
            response.ErrorMessage);

        await _configurationService.UpdateGraphSettingsAsync(graphSettings);
    }

    private async Task<(string UserMail, string MailboxId)?> ResolveTargetAsync(string? userMail, string? mailboxId)
    {
        if (!string.IsNullOrWhiteSpace(userMail) && !string.IsNullOrWhiteSpace(mailboxId))
        {
            return (userMail.Trim(), mailboxId.Trim());
        }

        var companies = await _configurationService.GetCompaniesAsync();
        var firstConfigured = companies
            .FirstOrDefault(company =>
                company.RecordType == Company.RecordTypeSetting &&
                !string.IsNullOrWhiteSpace(company.Mail) &&
                company.MailBox.Any(mailbox => !string.IsNullOrWhiteSpace(mailbox)));

        if (firstConfigured is null)
        {
            return null;
        }

        var targetMailbox = firstConfigured.MailBox
            .First(mailbox => !string.IsNullOrWhiteSpace(mailbox))
            .Trim();

        return (firstConfigured.Mail.Trim(), targetMailbox);
    }
}
