using System.Globalization;

namespace Timesheet.Api.Domain;

/// <summary>
/// Даты табеля — это календарные даты без времени и без часового пояса.
/// В Mongo BSON-даты всегда UTC, поэтому единственная точка преобразования — здесь:
/// наружу отдаём DateOnly, внутри и в базе — DateTime с Kind = Utc и нулевым временем.
/// </summary>
public static class Dates
{
    public static DateTime Utc(int year, int month, int day) =>
        new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime FromDateOnly(DateOnly date) => Utc(date.Year, date.Month, date.Day);

    public static DateOnly ToDateOnly(DateTime value) => DateOnly.FromDateTime(value);

    /// <summary>Начало месяца (включительно) для фильтра по периоду.</summary>
    public static DateTime MonthStart(int year, int month) => Utc(year, month, 1);

    /// <summary>Начало следующего месяца (исключительно) — верхняя граница диапазона.</summary>
    public static DateTime NextMonthStart(int year, int month) => MonthStart(year, month).AddMonths(1);

    public static string Format(DateTime value) => value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string FormatMonth(int year, int month) =>
        month.ToString("00", CultureInfo.InvariantCulture) + "." + year.ToString("0000", CultureInfo.InvariantCulture);
}
