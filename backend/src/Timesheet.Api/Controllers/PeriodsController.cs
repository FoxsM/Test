using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Application;
using Timesheet.Api.Contracts;

namespace Timesheet.Api.Controllers;

[ApiController]
[Route("api/periods")]
[Produces("application/json")]
public class PeriodsController : ControllerBase
{
    private readonly PeriodService _service;

    public PeriodsController(PeriodService service)
    {
        _service = service;
    }

    /// <summary>Список закрытых периодов — интерфейсу нужно знать, какие месяцы только для чтения.</summary>
    [HttpGet]
    public Task<List<ClosedPeriodDto>> List([FromQuery] int? year, CancellationToken cancellationToken) =>
        _service.ListAsync(year, cancellationToken);

    [HttpPost("close")]
    public Task<ClosedPeriodDto> Close([FromBody] PeriodRequest request, CancellationToken cancellationToken) =>
        _service.CloseAsync(request, cancellationToken);

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] PeriodRequest request, CancellationToken cancellationToken)
    {
        await _service.OpenAsync(request, cancellationToken);
        return NoContent();
    }
}
