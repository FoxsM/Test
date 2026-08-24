using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Application;
using Timesheet.Api.Contracts;

namespace Timesheet.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _service;

    public ReportsController(ReportService service)
    {
        _service = service;
    }

    /// <summary>Отчёт по проектам за месяц: часы, стоимость, бюджет, освоение, перерасход, итог.</summary>
    [HttpGet("projects")]
    public Task<ProjectReportResponse> Projects(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken) =>
        _service.GetProjectReportAsync(year, month, cancellationToken);
}
