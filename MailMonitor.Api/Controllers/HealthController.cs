using MailMonitor.Api.Contracts.Health;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Application.Abstractions.Graph;
using MailMonitor.Domain.Entities.Companies;
using Microsoft.AspNetCore.Mvc;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IConfigurationService _configurationService;
    private readonly IGraphClient _graphClient;

    public HealthController(
        IConfigurationService configurationService,
        IGraphClient graphClient)
    {
        _configurationService = configurationService;
        _graphClient = graphClient;
    }

    /// <summary>
    /// Validates Microsoft Graph connectivity against a mailbox target.
    /// </summary>
    /// <param name="userMail">Optional user mail. If omitted, the first configured company mailbox is used.</param>
    /// <param name="mailboxId">Optional mailbox id. If omitted, the first configured company mailbox is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Connectivity check succeeded.</response>
    /// <response code="503">Connectivity check failed or no mailbox target is configured.</response>
    [HttpGet("graph")]
    [ProducesResponseType(typeof(GraphConnectivityHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GraphConnectivityHealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GraphConnectivityHealthResponse>> CheckGraphConnectivityAsync(
        [FromQuery] string? userMail,
        [FromQuery] string? mailboxId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = await ResolveTargetAsync(userMail, mailboxId);
        if (target is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new GraphConnectivityHealthResponse(
                    DateTime.UtcNow,
                    false,
                    string.Empty,
                    string.Empty,
                    "GraphHealth.TargetNotConfigured",
                    "No mailbox target is available for Graph connectivity validation."));
        }

        var result = await _graphClient.CheckConnectivityAsync(target.Value.UserMail, target.Value.MailboxId, cancellationToken);

        var response = new GraphConnectivityHealthResponse(
            DateTime.UtcNow,
            result.IsSuccess,
            result.UserMail,
            result.MailboxId,
            result.ErrorCode,
            result.ErrorMessage);

        return result.IsSuccess
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private async Task<(string UserMail, string MailboxId)?> ResolveTargetAsync(string? userMail, string? mailboxId)
    {
        if (!string.IsNullOrWhiteSpace(userMail) && !string.IsNullOrWhiteSpace(mailboxId))
        {
            return (userMail.Trim(), mailboxId.Trim());
        }

        var companies = await _configurationService.GetCompaniesAsync();
        var firstConfigured = companies
            .FirstOrDefault(company =>
                company.RecordType == Company.RecordTypeSetting &&
                !string.IsNullOrWhiteSpace(company.Mail) &&
                company.MailBox.Any(mailbox => !string.IsNullOrWhiteSpace(mailbox)));

        if (firstConfigured is null)
        {
            return null;
        }

        var targetMailbox = firstConfigured.MailBox
            .First(mailbox => !string.IsNullOrWhiteSpace(mailbox))
            .Trim();

        return (firstConfigured.Mail.Trim(), targetMailbox);
    }
}
