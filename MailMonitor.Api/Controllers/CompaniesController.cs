using MailMonitor.Api.Contracts.Companies;
using MailMonitor.Application.Abstractions.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/companies")]
public sealed class CompaniesController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    public CompaniesController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyListItemResponse>>> GetAsync(
        [FromQuery] string? name,
        [FromQuery] string? mail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var companies = await _configurationService.GetCompaniesAsync();

        var filteredCompanies = companies
            .Where(company =>
                (string.IsNullOrWhiteSpace(name) ||
                 company.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(mail) ||
                 company.Mail.Contains(mail, StringComparison.OrdinalIgnoreCase)))
            .Select(company => new CompanyListItemResponse(
                company.Id,
                company.Name,
                company.Mail,
                company.StartFrom,
                company.StorageFolder,
                company.ReportOutputFolder,
                company.ProcessingTag))
            .ToList();

        return Ok(filteredCompanies);
    }
}
