using MailMonitor.Api.Contracts.Settings;
using MailMonitor.Application.Abstractions.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private static readonly char[] KeywordsSeparators = [',', ';', '\n', '\r'];

    private readonly IConfigurationService _configurationService;

    public SettingsController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    /// <summary>
    /// Gets global system settings.
    /// </summary>
    /// <response code="200">Current settings. Example: {"baseStorageFolder":"C:\\mail","mailSubjectKeywords":["invoice"],"processingTag":"ONBASE"}</response>
    [HttpGet]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingsResponse>> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = await _configurationService.GetSettingsAsync();

        var response = new SettingsResponse(
            settings.BaseStorageFolder,
            settings.MailSubjectKeywords
                .Split(KeywordsSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            settings.ProcessingTag);

        return Ok(response);
    }

    /// <summary>
    /// Updates global system settings.
    /// </summary>
    /// <response code="204">Settings updated.</response>
    /// <response code="400">Invalid payload. Example: {"errors":{"baseStorageFolder":["The BaseStorageFolder field is required."]}}</response>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateSettingsRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var currentSettings = await _configurationService.GetSettingsAsync();
        currentSettings.BaseStorageFolder = request.BaseStorageFolder;

        await _configurationService.UpdateSettingsAsync(currentSettings);

        return NoContent();
    }
}
