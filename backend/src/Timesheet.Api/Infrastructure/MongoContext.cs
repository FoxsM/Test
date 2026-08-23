using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Timesheet.Api.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>Точка доступа к коллекциям. Имена коллекций объявлены здесь и только здесь.</summary>
public class MongoContext
{
    public const string TimeEntriesCollection = "time_entries";
    public const string EmployeesCollection = "employees";
    public const string ProjectsCollection = "projects";
    public const string ClosedPeriodsCollection = "closed_periods";

    public MongoContext(IMongoClient client, IOptions<MongoOptions> options)
    {
        Database = client.GetDatabase(options.Value.Database);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<TimeEntry> TimeEntries => Database.GetCollection<TimeEntry>(TimeEntriesCollection);

    public IMongoCollection<Employee> Employees => Database.GetCollection<Employee>(EmployeesCollection);

    public IMongoCollection<Project> Projects => Database.GetCollection<Project>(ProjectsCollection);

    public IMongoCollection<ClosedPeriod> ClosedPeriods =>
        Database.GetCollection<ClosedPeriod>(ClosedPeriodsCollection);
}
