using System.Text.Json;
using MailMonitor.Api.Contracts.GraphSettings;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Domain.Entities.Graph;
using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/graph-settings")]
public sealed class GraphSettingsController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    public GraphSettingsController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
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
            graphSettings.GraphUserScopesJson);

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
}
