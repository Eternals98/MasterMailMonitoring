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

    [HttpGet]
    /// <summary>
    /// Obtiene configuración de Microsoft Graph.
    /// </summary>
    /// <response code="200">Configuración encontrada. El secreto se devuelve enmascarado.</response>
    /// <response code="404">No existe configuración cargada.</response>
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

    [HttpPut]
    /// <summary>
    /// Actualiza configuración de Microsoft Graph.
    /// </summary>
    /// <response code="204">Configuración actualizada.</response>
    /// <response code="400">Payload inválido o GraphUserScopesJson no válido.</response>
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
            ModelState.AddModelError(nameof(request), validationResult.Error.Message);
            return ValidationProblem(ModelState);
        }

        await _configurationService.UpdateGraphSettingsAsync(graphSettings);

        return NoContent();
    }

    private static bool IsValidScopesJson(string scopesJson)
    {
        try
        {
            var scopes = JsonSerializer.Deserialize<List<string>>(scopesJson);
            return scopes is not null;
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
            return secret;
        }

        return $"{new string('*', secret.Length - 4)}{secret[^4..]}";
    }
}
