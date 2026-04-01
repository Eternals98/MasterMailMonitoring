using MailMonitor.Api.Contracts.Settings;
using MailMonitor.Application.Abstractions.Configuration;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private static readonly char[] ValueSeparators = [',', ';', '|', '\n', '\r'];

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
            SplitValues(settings.MailSubjectKeywords),
            SplitValues(settings.MailSubjectKeywords),
            settings.ProcessingTag,
            settings.DefaultReportOutputFolder,
            SplitValues(settings.DefaultFileTypes),
            SplitValues(settings.DefaultAttachmentKeywords),
            settings.SchedulerTimeZoneId,
            settings.SchedulerFallbackCronExpression,
            settings.StorageMaxRetries,
            settings.StorageBaseDelayMs,
            settings.StorageMaxDelayMs,
            settings.GraphHealthCheckEnabled,
            settings.MailboxSearchEnabled,
            settings.ProcessingActionsEnabled,
            settings.UpdatedAtUtc,
            settings.UpdatedBy,
            settings.Revision);

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
        currentSettings.BaseStorageFolder = request.BaseStorageFolder.Trim();

        var requestedGlobalKeywords = request.GlobalSearchKeywords ?? request.MailSubjectKeywords;
        if (requestedGlobalKeywords is not null)
        {
            currentSettings.MailSubjectKeywords = JoinValues(requestedGlobalKeywords);
        }

        if (request.ProcessingTag is not null)
        {
            currentSettings.ProcessingTag = request.ProcessingTag.Trim();
        }

        if (request.DefaultReportOutputFolder is not null)
        {
            currentSettings.DefaultReportOutputFolder = request.DefaultReportOutputFolder.Trim();
        }

        if (request.DefaultFileTypes is not null)
        {
            currentSettings.DefaultFileTypes = JoinValues(request.DefaultFileTypes);
        }

        if (request.DefaultAttachmentKeywords is not null)
        {
            currentSettings.DefaultAttachmentKeywords = JoinValues(request.DefaultAttachmentKeywords);
        }

        if (request.SchedulerTimeZoneId is not null)
        {
            currentSettings.SchedulerTimeZoneId = request.SchedulerTimeZoneId.Trim();
        }

        if (request.SchedulerFallbackCronExpression is not null)
        {
            currentSettings.SchedulerFallbackCronExpression = request.SchedulerFallbackCronExpression.Trim();
        }

        if (request.StorageMaxRetries.HasValue)
        {
            currentSettings.StorageMaxRetries = request.StorageMaxRetries.Value;
        }

        if (request.StorageBaseDelayMs.HasValue)
        {
            currentSettings.StorageBaseDelayMs = request.StorageBaseDelayMs.Value;
        }

        if (request.StorageMaxDelayMs.HasValue)
        {
            currentSettings.StorageMaxDelayMs = request.StorageMaxDelayMs.Value;
        }

        if (request.GraphHealthCheckEnabled.HasValue)
        {
            currentSettings.GraphHealthCheckEnabled = request.GraphHealthCheckEnabled.Value;
        }

        if (request.MailboxSearchEnabled.HasValue)
        {
            currentSettings.MailboxSearchEnabled = request.MailboxSearchEnabled.Value;
        }

        if (request.ProcessingActionsEnabled.HasValue)
        {
            currentSettings.ProcessingActionsEnabled = request.ProcessingActionsEnabled.Value;
        }

        if (request.UpdatedBy is not null)
        {
            currentSettings.UpdatedBy = request.UpdatedBy.Trim();
        }

        var validation = currentSettings.Validate();
        if (validation.IsFailure)
        {
            ModelState.AddModelError(nameof(request), validation.Error.Name);
            return ValidationProblem(ModelState);
        }

        await _configurationService.UpdateSettingsAsync(currentSettings);

        return NoContent();
    }

    /// <summary>
    /// Validates if the API process can access a target storage folder.
    /// </summary>
    /// <response code="200">Validation result with read/write flags.</response>
    /// <response code="400">Invalid path format.</response>
    [HttpPost("storage-access/check")]
    [ProducesResponseType(typeof(CheckStorageAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CheckStorageAccessResponse>> CheckStorageAccessAsync(
        [FromBody] CheckStorageAccessRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var targetPath = request.Path?.Trim() ?? string.Empty;
        var normalizedPath = NormalizePath(targetPath);

        if (!IsLikelyAbsoluteOrUncPath(normalizedPath))
        {
            ModelState.AddModelError(nameof(request.Path), "Path debe ser una ruta absoluta local o UNC.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var fullPath = Path.GetFullPath(normalizedPath);
            Directory.CreateDirectory(fullPath);

            var canRead = false;
            var canWrite = false;

            try
            {
                _ = Directory.EnumerateFileSystemEntries(fullPath).Take(1).ToArray();
                canRead = true;
            }
            catch (UnauthorizedAccessException)
            {
                canRead = false;
            }

            var probeFile = Path.Combine(fullPath, $".mm_access_probe_{Guid.NewGuid():N}.tmp");
            try
            {
                await System.IO.File.WriteAllTextAsync(probeFile, "probe", Encoding.UTF8, cancellationToken);
                canWrite = true;
            }
            finally
            {
                if (System.IO.File.Exists(probeFile))
                {
                    System.IO.File.Delete(probeFile);
                }
            }

            var success = canRead && canWrite;

            return Ok(new CheckStorageAccessResponse(
                DateTime.UtcNow,
                targetPath,
                fullPath,
                Directory.Exists(fullPath),
                canRead,
                canWrite,
                success,
                success
                    ? "Acceso validado correctamente para lectura y escritura."
                    : "La carpeta no permite lectura/escritura completa para la API."));
        }
        catch (UnauthorizedAccessException)
        {
            return Ok(new CheckStorageAccessResponse(
                DateTime.UtcNow,
                targetPath,
                normalizedPath,
                false,
                false,
                false,
                false,
                "Permisos insuficientes para acceder a la carpeta."));
        }
        catch (Exception ex)
        {
            return Ok(new CheckStorageAccessResponse(
                DateTime.UtcNow,
                targetPath,
                normalizedPath,
                false,
                false,
                false,
                false,
                $"No fue posible validar la carpeta: {ex.Message}"));
        }
    }

    private static IReadOnlyCollection<string> SplitValues(string? values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return [];
        }

        return values
            .Split(ValueSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string JoinValues(IEnumerable<string> values)
    {
        return string.Join(",", values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2)
        {
            return trimmed;
        }

        var isDoubleQuoted = trimmed.StartsWith("\"", StringComparison.Ordinal) &&
                             trimmed.EndsWith("\"", StringComparison.Ordinal);
        var isSingleQuoted = trimmed.StartsWith("'", StringComparison.Ordinal) &&
                             trimmed.EndsWith("'", StringComparison.Ordinal);

        if (!isDoubleQuoted && !isSingleQuoted)
        {
            return trimmed;
        }

        return trimmed[1..^1].Trim();
    }

    private static bool IsLikelyAbsoluteOrUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.StartsWith(@"\\", StringComparison.Ordinal) ||
               (path.Length > 2 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'));
    }
}
