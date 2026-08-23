namespace Timesheet.Api.Infrastructure;

public class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string Database { get; set; } = "timesheet";
}
