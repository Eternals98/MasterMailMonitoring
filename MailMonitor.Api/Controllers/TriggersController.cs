using MailMonitor.Api.Contracts.Triggers;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Domain.Entities.Jobs;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace MailMonitor.Api.Controllers;

[ApiController]
[Route("api/triggers")]
public sealed class TriggersController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    public TriggersController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    /// <summary>
    /// Lists all configured triggers.
    /// </summary>
    /// <response code="200">Trigger list. Example: [{"id":"22222222-2222-2222-2222-222222222222","name":"Daily Report","cronExpression":"0 0 * * *"}]</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TriggerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TriggerResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var triggers = await _configurationService.GetTriggersAsync();

        var response = triggers
            .Select(trigger => new TriggerResponse(trigger.Id, trigger.Name, trigger.CronExpression))
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Creates a new trigger.
    /// </summary>
    /// <response code="201">Trigger created. Example request: {"name":"Daily Report","cronExpression":"0 0 * * *"}</response>
    /// <response code="400">Invalid payload or invalid cron expression. Example: {"errors":{"cronExpression":["The cron expression format is invalid."]}}</response>
    [HttpPost]
    [ProducesResponseType(typeof(TriggerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TriggerResponse>> CreateAsync(
        [FromBody] UpsertTriggerRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryNormalizeCronExpression(request.CronExpression, out var normalizedCronExpression))
        {
            ModelState.AddModelError(nameof(request.CronExpression), "The cron expression format is invalid.");
            return ValidationProblem(ModelState);
        }

        var triggerResult = Trigger.CreateValidated(request.Name, normalizedCronExpression);

        if (triggerResult.IsFailure)
        {
            ModelState.AddModelError(nameof(request), triggerResult.Error.Name);
            return ValidationProblem(ModelState);
        }

        await _configurationService.AddOrUpdateTriggerAsync(triggerResult.Value);

        var response = new TriggerResponse(
            triggerResult.Value.Id,
            triggerResult.Value.Name,
            triggerResult.Value.CronExpression);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = response.Id }, response);
    }

    /// <summary>
    /// Gets a trigger by id.
    /// </summary>
    /// <response code="200">Trigger found. Example: {"id":"22222222-2222-2222-2222-222222222222","name":"Daily Report","cronExpression":"0 0 * * *"}</response>
    /// <response code="404">Trigger not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TriggerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TriggerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var trigger = await _configurationService.GetTriggerByIdAsync(id);

        if (trigger is null)
        {
            return NotFound();
        }

        return Ok(new TriggerResponse(trigger.Id, trigger.Name, trigger.CronExpression));
    }

    /// <summary>
    /// Updates an existing trigger.
    /// </summary>
    /// <response code="204">Trigger updated.</response>
    /// <response code="400">Invalid payload or invalid cron expression. Example: {"errors":{"cronExpression":["The cron expression format is invalid."]}}</response>
    /// <response code="404">Trigger not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpsertTriggerRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "Trigger id is required.");
            return ValidationProblem(ModelState);
        }

        if (!TryNormalizeCronExpression(request.CronExpression, out var normalizedCronExpression))
        {
            ModelState.AddModelError(nameof(request.CronExpression), "The cron expression format is invalid.");
            return ValidationProblem(ModelState);
        }

        var existingTrigger = await _configurationService.GetTriggerByIdAsync(id);

        if (existingTrigger is null)
        {
            return NotFound();
        }

        var updateResult = existingTrigger.Update(request.Name, normalizedCronExpression, new Dictionary<string, string>());

        if (updateResult.IsFailure)
        {
            ModelState.AddModelError(nameof(request), updateResult.Error.Name);
            return ValidationProblem(ModelState);
        }

        await _configurationService.AddOrUpdateTriggerAsync(existingTrigger);

        return NoContent();
    }

    /// <summary>
    /// Deletes an existing trigger.
    /// </summary>
    /// <response code="204">Trigger deleted.</response>
    /// <response code="404">Trigger not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deleteResult = await _configurationService.DeleteTriggerAsync(id);

        return deleteResult.IsSuccess ? NoContent() : NotFound();
    }

    private static bool TryNormalizeCronExpression(string cronExpression, out string normalizedCronExpression)
    {
        normalizedCronExpression = string.Empty;

        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        var parts = cronExpression
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is 5)
        {
            normalizedCronExpression = $"0 {string.Join(' ', parts)}";
            return CronExpression.IsValidExpression(normalizedCronExpression);
        }

        if (parts.Length is 6 or 7)
        {
            normalizedCronExpression = string.Join(' ', parts);
            return CronExpression.IsValidExpression(normalizedCronExpression);
        }

        return false;
    }
}
