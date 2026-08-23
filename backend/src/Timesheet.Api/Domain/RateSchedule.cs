namespace Timesheet.Api.Domain;

/// <summary>
/// Выбор ставки, действовавшей на дату. Чистая функция без БД — она же
/// продублирована выражением $reduce в агрегации Mongo (см. TimesheetPipelines).
/// Обе реализации проверяются одними и теми же кейсами в тестах.
/// </summary>
public static class RateSchedule
{
    /// <summary>
    /// Возвращает ставку с максимальной датой начала, не превышающей <paramref name="date"/>,
    /// или null, если на эту дату ставок ещё не было.
    /// </summary>
    public static EmployeeRate? Effective(IEnumerable<EmployeeRate>? rates, DateTime date)
    {
        if (rates is null)
        {
            return null;
        }

        var day = date.Date;
        EmployeeRate? effective = null;

        foreach (var rate in rates)
        {
            if (rate.From.Date > day)
            {
                continue;
            }

            if (effective is null || rate.From.Date > effective.From.Date)
            {
                effective = rate;
            }
        }

        return effective;
    }

    /// <summary>Ставка на дату или ошибка бизнес-правила, если ставки нет.</summary>
    public static decimal RequireEffectiveValue(Employee employee, DateTime date)
    {
        var rate = Effective(employee.Rates, date);
        if (rate is null)
        {
            throw new BusinessRuleException(
                ErrorCodes.RateNotDefined,
                $"У сотрудника «{employee.FullName}» на {Dates.Format(date)} не задана часовая ставка. " +
                "Добавьте ставку, действующую с этой даты или раньше.");
        }

        return rate.Value;
    }
}
