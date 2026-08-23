using System.ComponentModel.DataAnnotations;

namespace Timesheet.Api.Contracts;

/// <summary>Параметры списка записей табеля.</summary>
public class TimeEntryListQuery
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string? EmployeeId { get; set; }

    public string? ProjectId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Тело запроса на создание (PUT) и изменение (POST /{id}) записи.</summary>
public class SaveTimeEntryRequest
{
    public string? EmployeeId { get; set; }

    public string? ProjectId { get; set; }

    /// <summary>Календарная дата в формате yyyy-MM-dd.</summary>
    public DateOnly? Date { get; set; }

    public decimal? Hours { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    /// <summary>
    /// Версия записи, которую редактировали. Обязательна при изменении:
    /// если в базе версия другая — значит, запись успели изменить, и мы отдаём 409.
    /// </summary>
    public int? Version { get; set; }
}

public class TimeEntryDto
{
    public string Id { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public decimal Hours { get; set; }

    /// <summary>Ставка, действовавшая на дату записи. null — ставку удалили задним числом.</summary>
    public decimal? Rate { get; set; }

    public decimal Amount { get; set; }

    public string? Comment { get; set; }

    public int Version { get; set; }

    /// <summary>Сколько всего часов у сотрудника в этот день по всем проектам.</summary>
    public decimal DayHours { get; set; }

    /// <summary>День помечен как переработка (больше 12 часов).</summary>
    public bool IsOvertimeDay { get; set; }

    /// <summary>Месяц записи закрыт бухгалтерией — редактировать нельзя.</summary>
    public bool IsPeriodClosed { get; set; }
}

public class TimeEntryListResponse
{
    public List<TimeEntryDto> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    /// <summary>Всего записей по фильтру (не на странице).</summary>
    public long Total { get; set; }

    public int TotalPages { get; set; }

    /// <summary>Итоги по всему отфильтрованному списку, а не по текущей странице.</summary>
    public TimeEntryTotals Totals { get; set; } = new();

    public bool IsPeriodClosed { get; set; }
}

public class TimeEntryTotals
{
    public decimal Hours { get; set; }

    public decimal Amount { get; set; }
}
