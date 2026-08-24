using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Application;
using Timesheet.Api.Contracts;

namespace Timesheet.Api.Controllers;

/// <summary>
/// Контроллер тонкий: разбор маршрута, вызов сервиса, код ответа.
/// Ни одной проверки бизнес-правил здесь нет.
/// </summary>
[ApiController]
[Route("api/time-entries")]
[Produces("application/json")]
public class TimeEntriesController : ControllerBase
{
    private readonly TimeEntryService _service;

    public TimeEntriesController(TimeEntryService service)
    {
        _service = service;
    }

    /// <summary>Постраничный список записей за месяц с фильтрами и итогами.</summary>
    [HttpGet]
    public Task<TimeEntryListResponse> List([FromQuery] TimeEntryListQuery query, CancellationToken cancellationToken) =>
        _service.ListAsync(query, cancellationToken);

    [HttpGet("{id}")]
    public Task<TimeEntryDto> Get(string id, CancellationToken cancellationToken) =>
        _service.GetAsync(id, cancellationToken);

    /// <summary>Создать запись.</summary>
    [HttpPut]
    public async Task<ActionResult<TimeEntryDto>> Create(
        [FromBody] SaveTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Изменить запись. Требует версию — иначе чужие правки затрутся молча.</summary>
    [HttpPost("{id}")]
    public Task<TimeEntryDto> Update(
        string id,
        [FromBody] SaveTimeEntryRequest request,
        CancellationToken cancellationToken) =>
        _service.UpdateAsync(id, request, cancellationToken);

    /// <summary>Удалить запись. version необязателен, но если передан — проверяется.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, version, cancellationToken);
        return NoContent();
    }
}
