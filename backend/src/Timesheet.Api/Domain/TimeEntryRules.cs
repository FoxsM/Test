namespace Timesheet.Api.Domain;

/// <summary>
/// Бизнес-правила табеля в чистом виде: на вход — уже загруженные данные,
/// на выход — либо ничего, либо BusinessRuleException с кодом и текстом для пользователя.
///
/// Обращений к базе здесь нет специально: правила проверяются юнит-тестами
/// без Mongo, а сервис отвечает только за то, чтобы принести данные для проверки.
/// </summary>
public static class TimeEntryRules
{
    /// <summary>Правило 4: в закрытом периоде записи нельзя создавать, изменять и удалять.</summary>
    public static void EnsurePeriodOpen(bool isClosed, DateTime date)
    {
        if (!isClosed)
        {
            return;
        }

        throw new BusinessRuleException(
            ErrorCodes.PeriodClosed,
            $"Период {Dates.FormatMonth(date.Year, date.Month)} закрыт бухгалтерией: " +
            "записи за этот месяц нельзя создавать, изменять и удалять.",
            StatusCodes.Status409Conflict,
            new Dictionary<string, object?>
            {
                ["year"] = date.Year,
                ["month"] = date.Month
            });
    }

    /// <summary>Правило 5: дата записи должна попадать в период проекта.</summary>
    public static void EnsureWithinProjectPeriod(Project project, DateTime date)
    {
        if (WorkTimeRules.IsWithinProjectPeriod(date, project.StartDate, project.EndDate))
        {
            return;
        }

        var period = project.EndDate is null
            ? $"с {Dates.Format(project.StartDate)} (без даты окончания)"
            : $"с {Dates.Format(project.StartDate)} по {Dates.Format(project.EndDate.Value)}";

        throw new BusinessRuleException(
            ErrorCodes.DateOutOfProjectPeriod,
            $"Дата {Dates.Format(date)} не попадает в период проекта {project.Code}: {period}.",
            StatusCodes.Status409Conflict,
            new Dictionary<string, object?>
            {
                ["projectCode"] = project.Code,
                ["startDate"] = Dates.ToDateOnly(project.StartDate).ToString("yyyy-MM-dd"),
                ["endDate"] = project.EndDate is null
                    ? null
                    : Dates.ToDateOnly(project.EndDate.Value).ToString("yyyy-MM-dd")
            });
    }

    /// <summary>
    /// Правило 2: суммарно за календарный день по всем проектам — не больше 24 часов.
    /// <paramref name="alreadyLogged"/> — часы этого сотрудника за эту дату без текущей записи.
    /// </summary>
    public static void EnsureWithinDailyLimit(
        Employee employee,
        DateTime date,
        decimal alreadyLogged,
        decimal requested)
    {
        var total = alreadyLogged + requested;

        if (!WorkTimeRules.ExceedsDailyLimit(total))
        {
            return;
        }

        throw new BusinessRuleException(
            ErrorCodes.DailyHoursLimitExceeded,
            $"У сотрудника «{employee.FullName}» за {Dates.Format(date)} уже учтено {Format(alreadyLogged)} ч " +
            $"по всем проектам. Ещё {Format(requested)} ч дадут {Format(total)} ч — это больше допустимых " +
            $"{Format(WorkTimeRules.MaxHoursPerDay)} ч за день.",
            StatusCodes.Status409Conflict,
            new Dictionary<string, object?>
            {
                ["alreadyLoggedHours"] = alreadyLogged,
                ["requestedHours"] = requested,
                ["totalHours"] = total,
                ["limitHours"] = WorkTimeRules.MaxHoursPerDay
            });
    }

    /// <summary>Правило 8: сохранение поверх чужих изменений запрещено.</summary>
    public static BusinessRuleException ConcurrentModification(int expectedVersion, int actualVersion) =>
        new(
            ErrorCodes.ConcurrentModification,
            "Запись изменил другой пользователь, пока вы её редактировали " +
            $"(вы работали с версией {expectedVersion}, в базе уже {actualVersion}). " +
            "Обновите данные и повторите изменение — иначе чужая правка потеряется.",
            StatusCodes.Status409Conflict,
            new Dictionary<string, object?>
            {
                ["expectedVersion"] = expectedVersion,
                ["actualVersion"] = actualVersion
            });

    private static string Format(decimal value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
}
