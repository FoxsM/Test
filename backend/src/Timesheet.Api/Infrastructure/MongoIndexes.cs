using MongoDB.Driver;
using Timesheet.Api.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Индексы создаются явно на старте приложения (CreateMany идемпотентен).
/// Обоснование каждого — в NOTES.md, кратко — здесь.
/// </summary>
public class MongoIndexes
{
    private readonly MongoContext _context;
    private readonly ILogger<MongoIndexes> _logger;

    public MongoIndexes(MongoContext context, ILogger<MongoIndexes> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        await _context.TimeEntries.Indexes.CreateManyAsync(
            new[]
            {
                // Основной рабочий индекс: выборка месяца + сортировка по дате,
                // плюс дофильтрация по сотруднику и проекту без дополнительного скана.
                new CreateIndexModel<TimeEntry>(
                    Builders<TimeEntry>.IndexKeys
                        .Ascending(x => x.Date)
                        .Ascending(x => x.EmployeeId)
                        .Ascending(x => x.ProjectId),
                    new CreateIndexOptions { Name = "date_employee_project" }),

                // Проверка лимита 24 часа за день: равенство по сотруднику + точная дата.
                new CreateIndexModel<TimeEntry>(
                    Builders<TimeEntry>.IndexKeys
                        .Ascending(x => x.EmployeeId)
                        .Ascending(x => x.Date),
                    new CreateIndexOptions { Name = "employee_date" }),

                // Фильтр «месяц + проект»: равенство по проекту эффективнее диапазона по дате.
                new CreateIndexModel<TimeEntry>(
                    Builders<TimeEntry>.IndexKeys
                        .Ascending(x => x.ProjectId)
                        .Ascending(x => x.Date),
                    new CreateIndexOptions { Name = "project_date" })
            },
            cancellationToken);

        await _context.Projects.Indexes.CreateOneAsync(
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Name = "code_unique", Unique = true }),
            cancellationToken: cancellationToken);

        await _context.Employees.Indexes.CreateOneAsync(
            new CreateIndexModel<Employee>(
                Builders<Employee>.IndexKeys.Ascending(x => x.FullName),
                new CreateIndexOptions { Name = "full_name" }),
            cancellationToken: cancellationToken);

        // Один документ на месяц: уникальность защищает от гонки при закрытии периода.
        await _context.ClosedPeriods.Indexes.CreateOneAsync(
            new CreateIndexModel<ClosedPeriod>(
                Builders<ClosedPeriod>.IndexKeys
                    .Ascending(x => x.Year)
                    .Ascending(x => x.Month),
                new CreateIndexOptions { Name = "year_month_unique", Unique = true }),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Индексы MongoDB проверены и созданы при необходимости");
    }
}
