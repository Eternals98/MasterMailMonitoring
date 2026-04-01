using MailMonitor.Api.Contracts.Companies;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Application.Abstractions.Graph;
using MailMonitor.Domain.Entities.Companies;
using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/companies")]
public sealed class CompaniesController : ControllerBase
{
    private const string GetCompanyByIdRouteName = "GetCompanyById";

    private readonly IConfigurationService _configurationService;
    private readonly IGraphClient _graphClient;

    public CompaniesController(
        IConfigurationService configurationService,
        IGraphClient graphClient)
    {
        _configurationService = configurationService;
        _graphClient = graphClient;
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
    /// Searches mailbox folders for a user and returns mailbox ids.
    /// </summary>
    /// <response code="200">Mailbox lookup matches.</response>
    /// <response code="400">Invalid query input.</response>
    /// <response code="503">Graph lookup unavailable.</response>
    [HttpGet("mailboxes/search")]
    [ProducesResponseType(typeof(IReadOnlyCollection<MailboxLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyCollection<MailboxLookupResponse>>> SearchMailboxesAsync(
        [FromQuery] string? userMail,
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedMail = userMail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedMail))
        {
            ModelState.AddModelError(nameof(userMail), "userMail is required.");
            return ValidationProblem(ModelState);
        }

        if (!string.IsNullOrWhiteSpace(query) && query.Trim().Length < 2)
        {
            ModelState.AddModelError(nameof(query), "query must have at least 2 characters.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _graphClient.SearchMailboxesAsync(normalizedMail, query ?? string.Empty, cancellationToken);

            var response = result
                .Select(item => new MailboxLookupResponse(item.Id, item.DisplayName, item.Path))
                .ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    errorCode = "GraphMailboxSearch.Unavailable",
                    errorMessage = ex.Message
                });
        }
    }

    /// <summary>
    /// Resolves mailbox metadata (name/path) for a list of mailbox ids.
    /// </summary>
    /// <response code="200">Resolved mailbox metadata.</response>
    /// <response code="400">Invalid request payload.</response>
    /// <response code="503">Graph lookup unavailable.</response>
    [HttpPost("mailboxes/resolve")]
    [ProducesResponseType(typeof(IReadOnlyCollection<MailboxLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyCollection<MailboxLookupResponse>>> ResolveMailboxesAsync(
        [FromBody] ResolveMailboxesRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedMail = request.UserMail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedMail))
        {
            ModelState.AddModelError(nameof(request.UserMail), "userMail is required.");
            return ValidationProblem(ModelState);
        }

        var ids = request.MailboxIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
        {
            ModelState.AddModelError(nameof(request.MailboxIds), "At least one mailbox id is required.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var resolved = await _graphClient.ResolveMailboxesAsync(normalizedMail, ids, cancellationToken);

            var response = resolved
                .Select(item => new MailboxLookupResponse(item.Id, item.DisplayName, item.Path))
                .ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    errorCode = "GraphMailboxResolve.Unavailable",
                    errorMessage = ex.Message
                });
        }
    }

    /// <summary>
    /// Verifies Graph connectivity for a mailbox and returns the latest emails (up to 5).
    /// </summary>
    /// <response code="200">Connectivity succeeded and recent emails were returned.</response>
    /// <response code="400">Invalid query input.</response>
    /// <response code="503">Connectivity check failed.</response>
    [HttpGet("mailboxes/recent")]
    [ProducesResponseType(typeof(MailboxRecentMessagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MailboxRecentMessagesResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MailboxRecentMessagesResponse>> GetRecentMailboxMessagesAsync(
        [FromQuery] string? userMail,
        [FromQuery] string? mailboxId,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedMail = userMail?.Trim() ?? string.Empty;
        var normalizedMailboxId = mailboxId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedMail))
        {
            ModelState.AddModelError(nameof(userMail), "userMail is required.");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(normalizedMailboxId))
        {
            ModelState.AddModelError(nameof(mailboxId), "mailboxId is required.");
            return ValidationProblem(ModelState);
        }

        var normalizedTake = take ?? 5;
        if (normalizedTake is < 1 or > 5)
        {
            ModelState.AddModelError(nameof(take), "take must be between 1 and 5.");
            return ValidationProblem(ModelState);
        }

        var connectivity = await _graphClient.CheckConnectivityAsync(normalizedMail, normalizedMailboxId, cancellationToken);
        if (!connectivity.IsSuccess)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MailboxRecentMessagesResponse(
                    DateTime.UtcNow,
                    false,
                    normalizedMail,
                    normalizedMailboxId,
                    [],
                    connectivity.ErrorCode,
                    connectivity.ErrorMessage));
        }

        var messages = await _graphClient.GetRecentMessagesAsync(
            normalizedMail,
            normalizedMailboxId,
            normalizedTake,
            cancellationToken);

        var response = new MailboxRecentMessagesResponse(
            DateTime.UtcNow,
            true,
            normalizedMail,
            normalizedMailboxId,
            messages
                .OrderByDescending(message => message.ReceivedDateTime)
                .Take(normalizedTake)
                .Select(message => new MailboxRecentMessageResponse(
                    message.Id ?? string.Empty,
                    message.Subject ?? "(sin asunto)",
                    message.ReceivedDateTime,
                    message.HasAttachments ?? false,
                    message.From?.EmailAddress?.Address ?? string.Empty))
                .ToList(),
            string.Empty,
            string.Empty);

        return Ok(response);
    }

    /// <summary>
    /// Gets a company by id.
    /// </summary>
    /// <response code="200">Company details. Example: {"id":"11111111-1111-1111-1111-111111111111","name":"Contoso","mail":"contoso@tenant.com"}</response>
    /// <response code="404">Company not found.</response>
    [HttpGet("{id:guid}", Name = GetCompanyByIdRouteName)]
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
            company.OverrideGlobalProcessingTag,
            company.OverrideGlobalStorageFolder,
            company.OverrideGlobalReportOutputFolder,
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
            request.ProcessingTag,
            request.OverrideGlobalProcessingTag,
            request.OverrideGlobalStorageFolder,
            request.OverrideGlobalReportOutputFolder);

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
            companyResult.Value.OverrideGlobalProcessingTag,
            companyResult.Value.OverrideGlobalStorageFolder,
            companyResult.Value.OverrideGlobalReportOutputFolder,
            companyResult.Value.RecordType,
            companyResult.Value.ProcessedSubject,
            companyResult.Value.ProcessedDate,
            companyResult.Value.ProcessedAttachmentsCount);

        return CreatedAtRoute(GetCompanyByIdRouteName, new { id = response.Id }, response);
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
            request.ProcessingTag,
            request.OverrideGlobalProcessingTag,
            request.OverrideGlobalStorageFolder,
            request.OverrideGlobalReportOutputFolder);

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
