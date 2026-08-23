namespace Timesheet.Api.Contracts;

public class ProjectReportResponse
{
    public int Year { get; set; }

    public int Month { get; set; }

    public List<ProjectReportRowDto> Rows { get; set; } = new();

    public ProjectReportTotalDto Total { get; set; } = new();
}

public class ProjectReportRowDto
{
    public string ProjectId { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public decimal Hours { get; set; }

    public decimal Amount { get; set; }

    public decimal Budget { get; set; }

    /// <summary>Процент освоения бюджета. null — бюджет не задан или нулевой.</summary>
    public decimal? Percent { get; set; }

    /// <summary>Освоено больше 100 % бюджета.</summary>
    public bool Overspent { get; set; }

    /// <summary>Освоено больше 80 % бюджета — риск перерасхода.</summary>
    public bool AtRisk { get; set; }

    public int Entries { get; set; }
}

public class ProjectReportTotalDto
{
    public decimal Hours { get; set; }

    public decimal Amount { get; set; }

    public int Projects { get; set; }

    public int Entries { get; set; }
}
