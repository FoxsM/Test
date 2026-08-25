using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;
using Timesheet.Api.Infrastructure;
using Timesheet.Api.Validation;

namespace Timesheet.Api.Application;

/// <summary>
/// Отчёт «стоимость трудозатрат по проектам за месяц».
///
/// Всё считает MongoDB одним конвейером: отбор месяца → ставка на дату записи →
/// стоимость записи → группировка по проекту → $facet на строки и итог.
/// В C# не выгружается ни одна запись табеля — только готовые строки по проектам.
/// </summary>
public class ReportService
{
    private readonly MongoContext _context;

    public ReportService(MongoContext context)
    {
        _context = context;
    }

    public async Task<ProjectReportResponse> GetProjectReportAsync(int year, int month, CancellationToken cancellationToken)
    {
        PeriodValidator.ValidateMonth(year, month);

        var stages = new List<BsonDocument>
        {
            TimesheetPipelines.MatchPeriod(
                Dates.MonthStart(year, month),
                Dates.NextMonthStart(year, month),
                employeeId: null,
                projectId: null),

            TimesheetPipelines.LookupEmployee(),
            TimesheetPipelines.Unwind("employee"),
            TimesheetPipelines.AddEffectiveRate(),
            TimesheetPipelines.AddAmount(),
            TimesheetPipelines.GroupByProject(),

            // Проект подтягивается уже после группировки: по одному документу на проект
            // вместо одного на каждую запись табеля.
            TimesheetPipelines.LookupProjectById(),
            TimesheetPipelines.Unwind("project"),
            TimesheetPipelines.AddBudgetPercent(),
            TimesheetPipelines.AddBudgetFlags(),

            TimesheetPipelines.ReportFacet()
        };

        var pipeline = new BsonDocumentStagePipelineDefinition<TimeEntry, ProjectReportFacet>(stages);

        using var cursor = await _context.TimeEntries.AggregateAsync(
            pipeline,
            new AggregateOptions { AllowDiskUse = true },
            cancellationToken);

        var facet = await cursor.FirstOrDefaultAsync(cancellationToken) ?? new ProjectReportFacet();
        var total = facet.Totals.FirstOrDefault() ?? new ProjectReportTotalRow();

        return new ProjectReportResponse
        {
            Year = year,
            Month = month,
            Rows = facet.Rows
                .Select(row => new ProjectReportRowDto
                {
                    ProjectId = row.ProjectId,
                    ProjectCode = row.ProjectCode,
                    ProjectName = row.ProjectName,
                    Hours = row.Hours,
                    Amount = row.Amount,
                    Budget = row.Budget,
                    Percent = row.Percent,
                    Overspent = row.Overspent,
                    AtRisk = row.AtRisk,
                    Entries = row.Entries
                })
                .ToList(),
            Total = new ProjectReportTotalDto
            {
                Hours = total.Hours,
                Amount = total.Amount,
                Projects = total.Projects,
                Entries = total.Entries
            }
        };
    }
}
