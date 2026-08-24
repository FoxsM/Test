using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Application;
using Timesheet.Api.Contracts;

namespace Timesheet.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class DictionariesController : ControllerBase
{
    private readonly DictionaryService _service;

    public DictionariesController(DictionaryService service)
    {
        _service = service;
    }

    [HttpGet("employees")]
    public Task<List<EmployeeDto>> Employees(CancellationToken cancellationToken) =>
        _service.GetEmployeesAsync(cancellationToken);

    [HttpGet("projects")]
    public Task<List<ProjectDto>> Projects(CancellationToken cancellationToken) =>
        _service.GetProjectsAsync(cancellationToken);

    /// <summary>
    /// Заменить историю ставок сотрудника (в ТЗ метода нет, добавлен для приёмочного сценария 8).
    /// </summary>
    [HttpPut("employees/{id}/rates")]
    public Task<EmployeeDto> ReplaceRates(
        string id,
        [FromBody] SaveRatesRequest request,
        CancellationToken cancellationToken) =>
        _service.ReplaceRatesAsync(id, request, cancellationToken);
}
