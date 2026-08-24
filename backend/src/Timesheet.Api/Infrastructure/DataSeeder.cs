using MongoDB.Driver;
using Timesheet.Api.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Тестовые данные из раздела «Приёмочные проверки» — ровно те, по которым
/// сверяются цифры отчёта. Идентификаторы фиксированы, чтобы на них можно было
/// ссылаться в curl-примерах README, а сид можно было прогонять сколько угодно раз.
/// </summary>
public class DataSeeder
{
    public const string IvanovId = "111111111111111111111111";
    public const string PetrovaId = "222222222222222222222222";
    public const string Project001Id = "aaaaaaaaaaaaaaaaaaaaaaaa";
    public const string Project002Id = "bbbbbbbbbbbbbbbbbbbbbbbb";

    private readonly MongoContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(MongoContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Чистим документы, но не коллекции: индексы созданы отдельным шагом и должны остаться.
        await _context.TimeEntries.DeleteManyAsync(Builders<TimeEntry>.Filter.Empty, cancellationToken);
        await _context.Employees.DeleteManyAsync(Builders<Employee>.Filter.Empty, cancellationToken);
        await _context.Projects.DeleteManyAsync(Builders<Project>.Filter.Empty, cancellationToken);
        await _context.ClosedPeriods.DeleteManyAsync(Builders<ClosedPeriod>.Filter.Empty, cancellationToken);

        var employees = new List<Employee>
        {
            new()
            {
                Id = IvanovId,
                FullName = "Иванов И. И.",
                Department = "Проектный",
                Rates = new List<EmployeeRate>
                {
                    new() { From = Dates.Utc(2026, 1, 1), Value = 500m },
                    new() { From = Dates.Utc(2026, 3, 1), Value = 600m }
                }
            },
            new()
            {
                Id = PetrovaId,
                FullName = "Петрова А. С.",
                Department = "Проектный",
                Rates = new List<EmployeeRate>
                {
                    new() { From = Dates.Utc(2026, 2, 1), Value = 700m }
                }
            }
        };

        var projects = new List<Project>
        {
            new()
            {
                Id = Project001Id,
                Code = "П-001",
                Name = "Реконструкция цеха",
                Budget = 20000m,
                StartDate = Dates.Utc(2026, 1, 1),
                EndDate = Dates.Utc(2026, 3, 31)
            },
            new()
            {
                Id = Project002Id,
                Code = "П-002",
                Name = "Инженерные сети",
                Budget = 5000m,
                StartDate = Dates.Utc(2026, 3, 1),
                EndDate = null
            }
        };

        var now = DateTime.UtcNow;

        var entries = new List<TimeEntry>
        {
            // 20.02.2026, Иванов, П-001, 8 ч → ставка 500 → 4 000 ₽
            NewEntry("cccccccccccccccccccccc01", IvanovId, Project001Id, Dates.Utc(2026, 2, 20), 8m, "Обследование", now),

            // 05.03.2026, Иванов, П-001, 8 ч → ставка уже 600 → 4 800 ₽
            NewEntry("cccccccccccccccccccccc02", IvanovId, Project001Id, Dates.Utc(2026, 3, 5), 8m, "Чертежи КМ", now),

            // 05.03.2026, Петрова, П-001, 4 ч → ставка 700 → 2 800 ₽
            NewEntry("cccccccccccccccccccccc03", PetrovaId, Project001Id, Dates.Utc(2026, 3, 5), 4m, "Смета", now),

            // 06.03.2026, Петрова, П-002, 10 ч → ставка 700 → 7 000 ₽
            NewEntry("cccccccccccccccccccccc04", PetrovaId, Project002Id, Dates.Utc(2026, 3, 6), 10m, "Наружные сети", now)
        };

        await _context.Employees.InsertManyAsync(employees, cancellationToken: cancellationToken);
        await _context.Projects.InsertManyAsync(projects, cancellationToken: cancellationToken);
        await _context.TimeEntries.InsertManyAsync(entries, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "База наполнена: {Employees} сотрудника, {Projects} проекта, {Entries} записи табеля. " +
            "Ожидаемый отчёт за март 2026: П-001 — 12 ч / 7 600 ₽ / 38 %, П-002 — 10 ч / 7 000 ₽ / 140 %, итого 22 ч / 14 600 ₽",
            employees.Count,
            projects.Count,
            entries.Count);
    }

    private static TimeEntry NewEntry(
        string id,
        string employeeId,
        string projectId,
        DateTime date,
        decimal hours,
        string comment,
        DateTime createdAt) => new()
    {
        Id = id,
        EmployeeId = employeeId,
        ProjectId = projectId,
        Date = date,
        Hours = hours,
        Comment = comment,
        Version = 1,
        CreatedAt = createdAt,
        CreatedBy = "seed"
    };
}
