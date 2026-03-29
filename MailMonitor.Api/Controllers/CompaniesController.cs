using MailMonitor.Api.Contracts.Companies;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Domain.Entities.Companies;
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

    /// <summary>
    /// Lists companies using optional filters by name and mail.
    /// </summary>
    /// <response code="200">Company list. Example: [{"id":"11111111-1111-1111-1111-111111111111","name":"Contoso","mail":"contoso@tenant.com"}]</response>
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

    /// <summary>
    /// Gets a company by id.
    /// </summary>
    /// <response code="200">Company details. Example: {"id":"11111111-1111-1111-1111-111111111111","name":"Contoso","mail":"contoso@tenant.com"}</response>
    /// <response code="404">Company not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDetailResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var company = await _configurationService.GetCompanyByIdAsync(id);
        if (company is null)
        {
            return NotFound();
        }

        var response = new CompanyDetailResponse(
            company.Id,
            company.Name,
            company.Mail,
            company.StartFrom,
            company.MailBox,
            company.FileTypes,
            company.AttachmentKeywords,
            company.StorageFolder,
            company.ReportOutputFolder,
            company.ProcessingTag,
            company.RecordType,
            company.ProcessedSubject,
            company.ProcessedDate,
            company.ProcessedAttachmentsCount);

        return Ok(response);
    }

    /// <summary>
    /// Creates a company.
    /// </summary>
    /// <response code="201">Company created. Example request: {"name":"Contoso","mail":"contoso@tenant.com","storageFolder":"C:\\mail","reportOutputFolder":"C:\\reports","processingTag":"ONBASE"}</response>
    /// <response code="400">Invalid payload or domain rule failure. Example: {"errors":{"name":["The Name field is required."]}}</response>
    [HttpPost]
    [ProducesResponseType(typeof(CompanyDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyDetailResponse>> CreateAsync(
        [FromBody] UpsertCompanyRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var companyResult = Company.CreateValidated(
            request.Name,
            request.Mail,
            request.StartFrom,
            request.MailBox,
            request.FileTypes,
            request.AttachmentKeywords,
            request.StorageFolder,
            request.ReportOutputFolder,
            request.ProcessingTag);

        if (companyResult.IsFailure)
        {
            ModelState.AddModelError(nameof(request), companyResult.Error.Name);
            return ValidationProblem(ModelState);
        }

        await _configurationService.AddOrUpdateCompanyAsync(companyResult.Value);

        var response = new CompanyDetailResponse(
            companyResult.Value.Id,
            companyResult.Value.Name,
            companyResult.Value.Mail,
            companyResult.Value.StartFrom,
            companyResult.Value.MailBox,
            companyResult.Value.FileTypes,
            companyResult.Value.AttachmentKeywords,
            companyResult.Value.StorageFolder,
            companyResult.Value.ReportOutputFolder,
            companyResult.Value.ProcessingTag,
            companyResult.Value.RecordType,
            companyResult.Value.ProcessedSubject,
            companyResult.Value.ProcessedDate,
            companyResult.Value.ProcessedAttachmentsCount);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = response.Id }, response);
    }

    /// <summary>
    /// Updates an existing company.
    /// </summary>
    /// <response code="204">Company updated.</response>
    /// <response code="400">Invalid payload including route/body id mismatch. Example: {"errors":{"id":["The route id must match body id."]}}</response>
    /// <response code="404">Company not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (id != request.Id)
        {
            ModelState.AddModelError(nameof(request.Id), "The route id must match body id.");
            return ValidationProblem(ModelState);
        }

        var company = await _configurationService.GetCompanyByIdAsync(id);
        if (company is null)
        {
            return NotFound();
        }

        var updateResult = company.Update(
            request.Name,
            request.Mail,
            request.StartFrom,
            request.MailBox,
            request.FileTypes,
            request.AttachmentKeywords,
            request.StorageFolder,
            request.ReportOutputFolder,
            request.ProcessingTag);

        if (updateResult.IsFailure)
        {
            ModelState.AddModelError(nameof(request), updateResult.Error.Name);
            return ValidationProblem(ModelState);
        }

        await _configurationService.AddOrUpdateCompanyAsync(company);

        return NoContent();
    }

    /// <summary>
    /// Deletes a company.
    /// </summary>
    /// <response code="204">Company deleted.</response>
    /// <response code="404">Company not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deleteResult = await _configurationService.DeleteCompanyAsync(id);

        return deleteResult.IsSuccess ? NoContent() : NotFound();
    }
}
