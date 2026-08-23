namespace Timesheet.Api.Contracts;

public class EmployeeDto
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public List<EmployeeRateDto> Rates { get; set; } = new();
}

public class EmployeeRateDto
{
    public DateOnly From { get; set; }

    public decimal Value { get; set; }
}

public class ProjectDto
{
    public string Id { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Budget { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
}

/// <summary>
/// Замена всей истории ставок сотрудника. В ТЗ такого метода нет,
/// но приёмочный сценарий 8 (поменять ставку задним числом и перестроить отчёт)
/// без него пришлось бы проверять руками в mongosh.
/// </summary>
public class SaveRatesRequest
{
    public List<EmployeeRateDto> Rates { get; set; } = new();
}

public class ClosedPeriodDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public DateTime ClosedAt { get; set; }

    public string ClosedBy { get; set; } = string.Empty;
}

public class PeriodRequest
{
    public int Year { get; set; }

    public int Month { get; set; }
}
