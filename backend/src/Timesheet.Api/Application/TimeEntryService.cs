using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;
using Timesheet.Api.Infrastructure;
using Timesheet.Api.Validation;

namespace Timesheet.Api.Application;

/// <summary>
/// Записи табеля: чтение через агрегацию, запись — с проверкой бизнес-правил.
///
/// Порядок проверок при записи фиксированный и одинаковый для create/update:
///   1) закрытый период (и старый, и новый месяц при переносе записи);
///   2) существование и период проекта;
///   3) наличие ставки на дату;
///   4) лимит 24 часа за день;
///   5) версия записи (оптимистичная блокировка).
/// Порядок важен: пользователю показывается первая по важности причина отказа.
/// </summary>
public class TimeEntryService
{
    private readonly MongoContext _context;
    private readonly PeriodService _periods;
    private readonly CurrentUser _currentUser;

    public TimeEntryService(MongoContext context, PeriodService periods, CurrentUser currentUser)
    {
        _context = context;
        _periods = periods;
        _currentUser = currentUser;
    }

    public async Task<TimeEntryListResponse> ListAsync(TimeEntryListQuery query, CancellationToken cancellationToken)
    {
        PeriodValidator.ValidateMonth(query.Year, query.Month);
        var (page, pageSize) = PeriodValidator.NormalizePaging(query.Page, query.PageSize);

        var employeeId = Ids.ParseOptional(query.EmployeeId, nameof(query.EmployeeId));
        var projectId = Ids.ParseOptional(query.ProjectId, nameof(query.ProjectId));

        var from = Dates.MonthStart(query.Year, query.Month);
        var to = Dates.NextMonthStart(query.Year, query.Month);
        var match = TimesheetPipelines.MatchPeriod(from, to, employeeId, projectId);

        // Страница: сначала sort/skip/limit по индексу, и только потом справочники и расчёт —
        // подтягивать сотрудников и проекты для всего месяца, чтобы показать 20 строк, незачем.
        var rows = await AggregateAsync<TimeEntryRow>(
            new[]
            {
                match,
                TimesheetPipelines.SortByDate(),
                TimesheetPipelines.Skip((page - 1) * pageSize),
                TimesheetPipelines.Limit(pageSize)
            }.Concat(CostStages()).Append(TimesheetPipelines.ProjectEntryRow()),
            cancellationToken);

        // Итоги — по всему отфильтрованному списку, считает Mongo.
        var totals = await AggregateAsync<TimeEntryTotalsRow>(
            new[] { match }.Concat(CostStages()).Append(TimesheetPipelines.GroupListTotals()),
            cancellationToken);

        var totalsRow = totals.FirstOrDefault() ?? new TimeEntryTotalsRow();
        var isPeriodClosed = await _periods.IsClosedAsync(query.Year, query.Month, cancellationToken);
        var items = await ToDtosAsync(rows, isPeriodClosed, cancellationToken);

        return new TimeEntryListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = totalsRow.Entries,
            TotalPages = totalsRow.Entries == 0 ? 0 : (int)Math.Ceiling(totalsRow.Entries / (double)pageSize),
            Totals = new TimeEntryTotals
            {
                Hours = totalsRow.Hours,
                Amount = Money.Round(totalsRow.Amount)
            },
            IsPeriodClosed = isPeriodClosed
        };
    }

    public async Task<TimeEntryDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var objectId = Ids.Parse(id, "id");

        var rows = await AggregateAsync<TimeEntryRow>(
            new[] { TimesheetPipelines.MatchId(objectId) }
                .Concat(CostStages())
                .Append(TimesheetPipelines.ProjectEntryRow()),
            cancellationToken);

        var row = rows.FirstOrDefault();
        if (row is null)
        {
            throw new EntityNotFoundException($"Запись табеля {id} не найдена");
        }

        var isPeriodClosed = await _periods.IsClosedAsync(row.Date.Year, row.Date.Month, cancellationToken);
        var dtos = await ToDtosAsync(new[] { row }, isPeriodClosed, cancellationToken);
        return dtos[0];
    }

    public async Task<TimeEntryDto> CreateAsync(SaveTimeEntryRequest request, CancellationToken cancellationToken)
    {
        TimeEntryValidator.Validate(request, requireVersion: false);

        var employeeId = Ids.Parse(request.EmployeeId, nameof(request.EmployeeId));
        var projectId = Ids.Parse(request.ProjectId, nameof(request.ProjectId));
        var date = Dates.FromDateOnly(request.Date!.Value);
        var hours = request.Hours!.Value;

        await _periods.EnsureOpenAsync(date, cancellationToken);
        var employee = await RequireEmployeeAsync(employeeId, cancellationToken);
        var project = await RequireProjectAsync(projectId, cancellationToken);

        TimeEntryRules.EnsureWithinProjectPeriod(project, date);
        RateSchedule.RequireEffectiveValue(employee, date);
        await EnsureDailyLimitAsync(employee, date, hours, excludeEntryId: null, cancellationToken);

        var entry = new TimeEntry
        {
            Id = ObjectId.GenerateNewId().ToString(),
            EmployeeId = employee.Id,
            ProjectId = project.Id,
            Date = date,
            Hours = hours,
            Comment = Normalize(request.Comment),
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.Name
        };

        await _context.TimeEntries.InsertOneAsync(entry, cancellationToken: cancellationToken);

        return await GetAsync(entry.Id, cancellationToken);
    }

    public async Task<TimeEntryDto> UpdateAsync(string id, SaveTimeEntryRequest request, CancellationToken cancellationToken)
    {
        TimeEntryValidator.Validate(request, requireVersion: true);

        var entryId = Ids.Parse(id, "id");
        var employeeId = Ids.Parse(request.EmployeeId, nameof(request.EmployeeId));
        var projectId = Ids.Parse(request.ProjectId, nameof(request.ProjectId));
        var date = Dates.FromDateOnly(request.Date!.Value);
        var hours = request.Hours!.Value;

        var existing = await FindEntryAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException($"Запись табеля {id} не найдена");

        // Запись нельзя ни вынести из закрытого месяца, ни занести в него.
        await _periods.EnsureOpenAsync(existing.Date, cancellationToken);
        await _periods.EnsureOpenAsync(date, cancellationToken);

        var employee = await RequireEmployeeAsync(employeeId, cancellationToken);
        var project = await RequireProjectAsync(projectId, cancellationToken);

        TimeEntryRules.EnsureWithinProjectPeriod(project, date);
        RateSchedule.RequireEffectiveValue(employee, date);
        await EnsureDailyLimitAsync(employee, date, hours, excludeEntryId: entryId, cancellationToken);

        var filter = Builders<TimeEntry>.Filter.And(
            Builders<TimeEntry>.Filter.Eq(x => x.Id, id),
            Builders<TimeEntry>.Filter.Eq(x => x.Version, request.Version!.Value));

        var update = Builders<TimeEntry>.Update
            .Set(x => x.EmployeeId, employee.Id)
            .Set(x => x.ProjectId, project.Id)
            .Set(x => x.Date, date)
            .Set(x => x.Hours, hours)
            .Set(x => x.Comment, Normalize(request.Comment))
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedBy, _currentUser.Name)
            .Inc(x => x.Version, 1);

        var result = await _context.TimeEntries.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            await ThrowConcurrencyOrNotFoundAsync(id, request.Version!.Value, cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(string id, int? version, CancellationToken cancellationToken)
    {
        var existing = await FindEntryAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException($"Запись табеля {id} не найдена");

        await _periods.EnsureOpenAsync(existing.Date, cancellationToken);

        var filter = Builders<TimeEntry>.Filter.Eq(x => x.Id, id);
        if (version.HasValue)
        {
            filter = Builders<TimeEntry>.Filter.And(
                filter,
                Builders<TimeEntry>.Filter.Eq(x => x.Version, version.Value));
        }

        var result = await _context.TimeEntries.DeleteOneAsync(filter, cancellationToken);

        if (result.DeletedCount == 0)
        {
            await ThrowConcurrencyOrNotFoundAsync(id, version ?? existing.Version, cancellationToken);
        }
    }

    // --- бизнес-правила: сервис приносит данные, решение принимает TimeEntryRules ---

    private async Task EnsureDailyLimitAsync(
        Employee employee,
        DateTime date,
        decimal hours,
        ObjectId? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var alreadyLogged = await SumDayHoursAsync(
            ObjectId.Parse(employee.Id),
            date,
            excludeEntryId,
            cancellationToken);

        TimeEntryRules.EnsureWithinDailyLimit(employee, date, alreadyLogged, hours);
    }

    // --- работа с базой ---

    private async Task<decimal> SumDayHoursAsync(
        ObjectId employeeId,
        DateTime date,
        ObjectId? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var filter = new BsonDocument
        {
            { "employeeId", employeeId },
            { "date", date }
        };

        if (excludeEntryId.HasValue)
        {
            filter.Add("_id", new BsonDocument("$ne", excludeEntryId.Value));
        }

        var rows = await AggregateAsync<TimeEntryTotalsRow>(
            new[]
            {
                new BsonDocument("$match", filter),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "hours", new BsonDocument("$sum", "$hours") }
                })
            },
            cancellationToken);

        return rows.FirstOrDefault()?.Hours ?? 0m;
    }

    private async Task<TimeEntry?> FindEntryAsync(string id, CancellationToken cancellationToken)
    {
        Ids.Parse(id, "id");

        return await _context.TimeEntries
            .Find(Builders<TimeEntry>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Employee> RequireEmployeeAsync(ObjectId employeeId, CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .Find(Builders<Employee>.Filter.Eq(x => x.Id, employeeId.ToString()))
            .FirstOrDefaultAsync(cancellationToken);

        return employee ?? throw new EntityNotFoundException($"Сотрудник {employeeId} не найден");
    }

    private async Task<Project> RequireProjectAsync(ObjectId projectId, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Find(Builders<Project>.Filter.Eq(x => x.Id, projectId.ToString()))
            .FirstOrDefaultAsync(cancellationToken);

        return project ?? throw new EntityNotFoundException($"Проект {projectId} не найден");
    }

    private async Task ThrowConcurrencyOrNotFoundAsync(string id, int expectedVersion, CancellationToken cancellationToken)
    {
        var current = await FindEntryAsync(id, cancellationToken);

        if (current is null)
        {
            throw new EntityNotFoundException(
                $"Запись табеля {id} не найдена — возможно, её удалил другой пользователь");
        }

        throw TimeEntryRules.ConcurrentModification(expectedVersion, current.Version);
    }

    /// <summary>Общие для списка и отчёта стадии: справочники + ставка на дату + стоимость.</summary>
    private static IEnumerable<BsonDocument> CostStages() => new[]
    {
        TimesheetPipelines.LookupEmployee(),
        TimesheetPipelines.Unwind("employee"),
        TimesheetPipelines.LookupProject(),
        TimesheetPipelines.Unwind("project"),
        TimesheetPipelines.AddEffectiveRate(),
        TimesheetPipelines.AddAmount()
    };

    private async Task<List<T>> AggregateAsync<T>(IEnumerable<BsonDocument> stages, CancellationToken cancellationToken)
    {
        var pipeline = new BsonDocumentStagePipelineDefinition<TimeEntry, T>(stages.ToList());

        using var cursor = await _context.TimeEntries.AggregateAsync(
            pipeline,
            new AggregateOptions { AllowDiskUse = true },
            cancellationToken);

        return await cursor.ToListAsync(cancellationToken);
    }

    // --- сборка DTO ---

    private async Task<List<TimeEntryDto>> ToDtosAsync(
        IReadOnlyCollection<TimeEntryRow> rows,
        bool isPeriodClosed,
        CancellationToken cancellationToken)
    {
        var dayHours = await LoadDayHoursAsync(rows, cancellationToken);

        return rows.Select(row =>
        {
            dayHours.TryGetValue((row.EmployeeId, row.Date), out var hoursInDay);

            return new TimeEntryDto
            {
                Id = row.Id,
                EmployeeId = row.EmployeeId,
                EmployeeName = row.EmployeeName,
                ProjectId = row.ProjectId,
                ProjectCode = row.ProjectCode,
                ProjectName = row.ProjectName,
                Date = Dates.ToDateOnly(row.Date),
                Hours = row.Hours,
                Rate = row.Rate,
                Amount = row.Amount,
                Comment = row.Comment,
                Version = row.Version,
                DayHours = hoursInDay,
                IsOvertimeDay = WorkTimeRules.IsOvertime(hoursInDay),
                IsPeriodClosed = isPeriodClosed
            };
        }).ToList();
    }

    /// <summary>
    /// Часы по дням для строк страницы. Переработка — свойство дня, а не записи,
    /// поэтому её нельзя посчитать внутри строки: нужен итог по всем проектам сотрудника за этот день.
    /// </summary>
    private async Task<Dictionary<(string EmployeeId, DateTime Date), decimal>> LoadDayHoursAsync(
        IReadOnlyCollection<TimeEntryRow> rows,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, DateTime), decimal>();

        if (rows.Count == 0)
        {
            return result;
        }

        var employeeIds = rows
            .Select(x => x.EmployeeId)
            .Distinct()
            .Where(Ids.IsValid)
            .Select(ObjectId.Parse)
            .ToList();

        var dates = rows.Select(x => x.Date).Distinct().ToList();

        if (employeeIds.Count == 0)
        {
            return result;
        }

        var match = new BsonDocument("$match", new BsonDocument
        {
            { "employeeId", new BsonDocument("$in", new BsonArray(employeeIds)) },
            { "date", new BsonDocument("$in", new BsonArray(dates)) }
        });

        var groups = await AggregateAsync<DayHoursRow>(
            new[] { match, TimesheetPipelines.GroupDayHours() },
            cancellationToken);

        foreach (var group in groups)
        {
            result[(group.Id.EmployeeId, group.Id.Date)] = group.Hours;
        }

        return result;
    }

    private static string? Normalize(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
}
