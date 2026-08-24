using Timesheet.Api.Domain;
using Xunit;

namespace Timesheet.Tests;

/// <summary>
/// Контрольный расчёт отчёта по приёмочным данным.
///
/// Сам отчёт считает MongoDB, и агрегацию юнит-тестом не проверишь — но арифметика
/// в конвейере ($reduce по ставкам, $round до копеек, суммы по проекту) обязана совпадать
/// с доменной. Этот тест фиксирует ожидаемые цифры из задания: если Money или RateSchedule
/// поменяются, он упадёт раньше, чем расхождение заметит бухгалтерия.
/// </summary>
public class AcceptanceReportTests
{
    private static readonly Employee Ivanov = new()
    {
        Id = "111111111111111111111111",
        FullName = "Иванов И. И.",
        Rates = new List<EmployeeRate>
        {
            new() { From = Dates.Utc(2026, 1, 1), Value = 500m },
            new() { From = Dates.Utc(2026, 3, 1), Value = 600m }
        }
    };

    private static readonly Employee Petrova = new()
    {
        Id = "222222222222222222222222",
        FullName = "Петрова А. С.",
        Rates = new List<EmployeeRate>
        {
            new() { From = Dates.Utc(2026, 2, 1), Value = 700m }
        }
    };

    private static readonly Project Project001 = new()
    {
        Code = "П-001",
        Budget = 20000m,
        StartDate = Dates.Utc(2026, 1, 1),
        EndDate = Dates.Utc(2026, 3, 31)
    };

    private static readonly Project Project002 = new()
    {
        Code = "П-002",
        Budget = 5000m,
        StartDate = Dates.Utc(2026, 3, 1),
        EndDate = null
    };

    private static IReadOnlyList<(Employee Employee, Project Project, DateTime Date, decimal Hours)> Seed() => new[]
    {
        (Ivanov, Project001, Dates.Utc(2026, 2, 20), 8m),
        (Ivanov, Project001, Dates.Utc(2026, 3, 5), 8m),
        (Petrova, Project001, Dates.Utc(2026, 3, 5), 4m),
        (Petrova, Project002, Dates.Utc(2026, 3, 6), 10m)
    };

    private static decimal Cost(Employee employee, DateTime date, decimal hours) =>
        Money.Cost(hours, RateSchedule.RequireEffectiveValue(employee, date));

    [Fact]
    public void Стоимость_каждой_записи_совпадает_с_таблицей_задания()
    {
        Assert.Equal(4000m, Cost(Ivanov, Dates.Utc(2026, 2, 20), 8m));
        Assert.Equal(4800m, Cost(Ivanov, Dates.Utc(2026, 3, 5), 8m));
        Assert.Equal(2800m, Cost(Petrova, Dates.Utc(2026, 3, 5), 4m));
        Assert.Equal(7000m, Cost(Petrova, Dates.Utc(2026, 3, 6), 10m));
    }

    [Fact]
    public void Отчёт_за_март_2026()
    {
        var rows = Report(2026, 3);

        var p001 = rows.Single(row => row.Code == "П-001");
        Assert.Equal(12m, p001.Hours);
        Assert.Equal(7600m, p001.Amount);
        Assert.Equal(38m, p001.Percent);
        Assert.False(p001.Overspent);
        Assert.False(p001.AtRisk);

        var p002 = rows.Single(row => row.Code == "П-002");
        Assert.Equal(10m, p002.Hours);
        Assert.Equal(7000m, p002.Amount);
        Assert.Equal(140m, p002.Percent);
        Assert.True(p002.Overspent);
        Assert.True(p002.AtRisk);

        Assert.Equal(22m, rows.Sum(row => row.Hours));
        Assert.Equal(14600m, rows.Sum(row => row.Amount));
    }

    [Fact]
    public void Отчёт_за_февраль_2026()
    {
        var rows = Report(2026, 2);

        var row = Assert.Single(rows);
        Assert.Equal("П-001", row.Code);
        Assert.Equal(8m, row.Hours);
        Assert.Equal(4000m, row.Amount);
        Assert.Equal(20m, row.Percent);
    }

    [Fact]
    public void После_изменения_ставки_задним_числом_отчёт_за_март_меняется()
    {
        // Сценарий 8: ставка Иванова с 01.03.2026 становится 650 ₽.
        var ivanov = new Employee
        {
            FullName = Ivanov.FullName,
            Rates = new List<EmployeeRate>
            {
                new() { From = Dates.Utc(2026, 1, 1), Value = 500m },
                new() { From = Dates.Utc(2026, 3, 1), Value = 650m }
            }
        };

        Assert.Equal(5200m, Cost(ivanov, Dates.Utc(2026, 3, 5), 8m));
    }

    private static List<ReportRow> Report(int year, int month) => Seed()
        .Where(entry => entry.Date.Year == year && entry.Date.Month == month)
        .GroupBy(entry => entry.Project)
        .Select(group =>
        {
            var amount = group.Sum(entry => Cost(entry.Employee, entry.Date, entry.Hours));
            var percent = Money.Round(amount / group.Key.Budget * 100m);

            return new ReportRow(
                group.Key.Code,
                group.Sum(entry => entry.Hours),
                amount,
                percent,
                percent > 100m,
                percent > 80m);
        })
        .OrderBy(row => row.Code)
        .ToList();

    private record ReportRow(
        string Code,
        decimal Hours,
        decimal Amount,
        decimal Percent,
        bool Overspent,
        bool AtRisk);
}
