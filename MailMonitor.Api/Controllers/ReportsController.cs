using MailMonitor.Api.Contracts.Statistics;
using MailMonitor.Application.Abstractions.Reporting;
using MailMonitor.Domain.Entities.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly IEmailStatisticsExporter _emailStatisticsExporter;

    public ReportsController(
        IReportingService reportingService,
        IEmailStatisticsExporter emailStatisticsExporter)
    {
        _reportingService = reportingService;
        _emailStatisticsExporter = emailStatisticsExporter;
    }

    /// <summary>
    /// Exporta estadísticas de correo a un archivo Excel.
    /// </summary>
    /// <param name="query">Filtros opcionales: from, to, company, processed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Archivo .xlsx con los resultados.</returns>
    /// <response code="200">Archivo Excel generado correctamente.</response>
    /// <response code="400">Rango de fechas inválido. Ejemplo: {"errors":{"to":["'to' must be greater than or equal to 'from'."]}}</response>
    [HttpGet("export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] EmailStatisticsQueryRequest query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query.From.HasValue && query.To.HasValue && query.To.Value < query.From.Value)
        {
            ModelState.AddModelError(nameof(query.To), "'to' must be greater than or equal to 'from'.");
            return ValidationProblem(ModelState);
        }

        IEnumerable<EmailProcessStatistic> statistics = _reportingService.GetEmailStatistics();

        if (query.From.HasValue)
        {
            statistics = statistics.Where(item => item.Date >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            statistics = statistics.Where(item => item.Date <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Company))
        {
            statistics = statistics.Where(item => item.CompanyName.Contains(query.Company, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Processed.HasValue)
        {
            statistics = statistics.Where(item => item.Processed == query.Processed.Value);
        }

        var filteredStatistics = statistics
            .OrderByDescending(item => item.Date)
            .ToList();

        var outputPath = Path.Combine(Path.GetTempPath(), $"email-statistics-{DateTime.UtcNow:yyyyMMddHHmmssfff}.xlsx");

        await _emailStatisticsExporter.ExportAsync(filteredStatistics, outputPath, cancellationToken);

        var bytes = await System.IO.File.ReadAllBytesAsync(outputPath, cancellationToken);
        System.IO.File.Delete(outputPath);

        var fileName = $"email-statistics-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
