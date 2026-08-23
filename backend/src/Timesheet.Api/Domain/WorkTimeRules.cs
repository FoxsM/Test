namespace Timesheet.Api.Domain;

/// <summary>
/// Числовые правила табеля в одном месте: и валидация запроса, и сервис,
/// и тесты ссылаются на эти константы, а не на литералы по коду.
/// </summary>
public static class WorkTimeRules
{
    /// <summary>Не больше 24 часов в одной записи.</summary>
    public const decimal MaxHoursPerEntry = 24m;

    /// <summary>Суммарно по всем проектам за один календарный день — не больше 24 часов.</summary>
    public const decimal MaxHoursPerDay = 24m;

    /// <summary>Больше 12 часов за день — запись сохраняется, день помечается как переработка.</summary>
    public const decimal OvertimeThresholdPerDay = 12m;

    /// <summary>Часы задаются с шагом в полчаса.</summary>
    public const decimal HoursStep = 0.5m;

    public static bool IsPositive(decimal hours) => hours > 0m;

    public static bool IsMultipleOfStep(decimal hours) => hours % HoursStep == 0m;

    public static bool FitsSingleEntry(decimal hours) => hours <= MaxHoursPerEntry;

    /// <summary>Итог за день превысил допустимые 24 часа.</summary>
    public static bool ExceedsDailyLimit(decimal dayHours) => dayHours > MaxHoursPerDay;

    /// <summary>Итог за день — переработка (больше 12 часов).</summary>
    public static bool IsOvertime(decimal dayHours) => dayHours > OvertimeThresholdPerDay;

    /// <summary>
    /// Дата записи попадает в период проекта: не раньше начала и не позже окончания,
    /// если окончание задано. Сравнение — по календарным датам.
    /// </summary>
    public static bool IsWithinProjectPeriod(DateTime date, DateTime projectStart, DateTime? projectEnd)
    {
        if (date.Date < projectStart.Date)
        {
            return false;
        }

        return projectEnd is null || date.Date <= projectEnd.Value.Date;
    }
}
