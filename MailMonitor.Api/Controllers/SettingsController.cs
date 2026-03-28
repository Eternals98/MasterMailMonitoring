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
}
