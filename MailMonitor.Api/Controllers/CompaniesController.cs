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

    [HttpGet]
    /// <summary>
    /// Lista compañías con filtros opcionales por nombre y correo.
    /// </summary>
    /// <response code="200">Listado de compañías. Ejemplo: [{"id":"11111111-1111-1111-1111-111111111111","name":"Contoso","mail":"contoso@tenant.com"}]</response>
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

    [HttpGet("{id:guid}")]
    /// <summary>
    /// Obtiene una compañía por identificador.
    /// </summary>
    /// <response code="200">Detalle de compañía.</response>
    /// <response code="404">No existe una compañía con el id indicado.</response>
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

    [HttpPost]
    /// <summary>
    /// Crea una compañía.
    /// </summary>
    /// <response code="201">Compañía creada. Ejemplo body: {"name":"Contoso","mail":"contoso@tenant.com","storageFolder":"c:\\mail\\contoso"}</response>
    /// <response code="400">Payload inválido o reglas de dominio incumplidas.</response>
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
            ModelState.AddModelError(nameof(request), companyResult.Error.Message);
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

    [HttpPut("{id:guid}")]
    /// <summary>
    /// Actualiza una compañía existente.
    /// </summary>
    /// <response code="204">Compañía actualizada.</response>
    /// <response code="400">Payload inválido (incluye mismatch id ruta/body).</response>
    /// <response code="404">No existe la compañía.</response>
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
            ModelState.AddModelError(nameof(request), updateResult.Error.Message);
            return ValidationProblem(ModelState);
        }

        await _configurationService.AddOrUpdateCompanyAsync(company);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    /// <summary>
    /// Elimina una compañía.
    /// </summary>
    /// <response code="204">Compañía eliminada.</response>
    /// <response code="404">No existe la compañía.</response>
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
