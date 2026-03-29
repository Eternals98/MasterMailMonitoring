using MailMonitor.Api.Contracts.Triggers;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Domain.Entities.Jobs;
using Microsoft.AspNetCore.Mvc;

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

        if (!IsValidCronExpression(request.CronExpression))
        {
            ModelState.AddModelError(nameof(request.CronExpression), "The cron expression format is invalid.");
            return ValidationProblem(ModelState);
        }

        var triggerResult = Trigger.CreateValidated(request.Name, request.CronExpression);

        if (triggerResult.IsFailure)
        {
            ModelState.AddModelError(nameof(request), triggerResult.Error.Message);
            return ValidationProblem(ModelState);
        }

        await _configurationService.AddOrUpdateTriggerAsync(triggerResult.Value);

        var response = new TriggerResponse(
            triggerResult.Value.Id,
            triggerResult.Value.Name,
            triggerResult.Value.CronExpression);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = response.Id }, response);
    }

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

        if (!IsValidCronExpression(request.CronExpression))
        {
            ModelState.AddModelError(nameof(request.CronExpression), "The cron expression format is invalid.");
            return ValidationProblem(ModelState);
        }

        var existingTrigger = await _configurationService.GetTriggerByIdAsync(id);

        if (existingTrigger is null)
        {
            return NotFound();
        }

        var updateResult = existingTrigger.Update(request.Name, request.CronExpression, new Dictionary<string, string>());

        if (updateResult.IsFailure)
        {
            ModelState.AddModelError(nameof(request), updateResult.Error.Message);
            return ValidationProblem(ModelState);
        }

        await _configurationService.AddOrUpdateTriggerAsync(existingTrigger);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deleteResult = await _configurationService.DeleteTriggerAsync(id);

        return deleteResult.IsSuccess ? NoContent() : NotFound();
    }

    private static bool IsValidCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        var parts = cronExpression
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length is 5 or 6 or 7;
    }
}
