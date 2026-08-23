using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Строки, которые возвращают агрегации. Это внутренний слой:
/// наружу из сервисов уходят DTO из Contracts, а не эти типы.
/// </summary>
public class TimeEntryRow
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string EmployeeId { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ProjectId { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public decimal Hours { get; set; }

    public decimal? Rate { get; set; }

    public decimal Amount { get; set; }

    public string? Comment { get; set; }

    public int Version { get; set; }
}

public class TimeEntryTotalsRow
{
    public decimal Hours { get; set; }

    public decimal Amount { get; set; }

    public int Entries { get; set; }
}

public class DayHoursRow
{
    public DayHoursKey Id { get; set; } = new();

    public decimal Hours { get; set; }
}

public class DayHoursKey
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string EmployeeId { get; set; } = string.Empty;

    public DateTime Date { get; set; }
}

public class ProjectReportRow
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProjectId { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public decimal Hours { get; set; }

    public decimal Amount { get; set; }

    public decimal Budget { get; set; }

    public decimal? Percent { get; set; }

    public bool Overspent { get; set; }

    public bool AtRisk { get; set; }

    public int Entries { get; set; }
}

public class ProjectReportTotalRow
{
    public decimal Hours { get; set; }

    public decimal Amount { get; set; }

    public int Projects { get; set; }

    public int Entries { get; set; }
}

/// <summary>Результат $facet: строки отчёта и итоговая строка одним запросом.</summary>
public class ProjectReportFacet
{
    public List<ProjectReportRow> Rows { get; set; } = new();

    public List<ProjectReportTotalRow> Totals { get; set; } = new();
}
