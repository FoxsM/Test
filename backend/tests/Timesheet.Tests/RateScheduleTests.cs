using Timesheet.Api.Domain;
using Xunit;

namespace Timesheet.Tests;

/// <summary>
/// Правило 1: стоимость считается по ставке, действовавшей на дату записи, а не по текущей.
/// Данные — из приёмочных проверок: Иванов 500 ₽ с 01.01.2026 и 600 ₽ с 01.03.2026.
/// </summary>
public class RateScheduleTests
{
    private static Employee Ivanov() => new()
    {
        FullName = "Иванов И. И.",
        Rates = new List<EmployeeRate>
        {
            // Намеренно в «неправильном» порядке: код не должен полагаться на сортировку массива.
            new() { From = Dates.Utc(2026, 3, 1), Value = 600m },
            new() { From = Dates.Utc(2026, 1, 1), Value = 500m }
        }
    };

    [Theory]
    [InlineData(2026, 1, 1, 500)]    // первый день действия первой ставки
    [InlineData(2026, 2, 20, 500)]   // середина периода первой ставки
    [InlineData(2026, 2, 28, 500)]   // последний день перед сменой
    [InlineData(2026, 3, 1, 600)]    // ставка действует с указанной даты включительно
    [InlineData(2026, 3, 5, 600)]    // приёмочная проверка: запись 05.03 идёт уже по 600
    [InlineData(2027, 12, 31, 600)]  // последняя ставка действует бессрочно
    public void Возвращает_ставку_действовавшую_на_дату(int year, int month, int day, int expected)
    {
        var rate = RateSchedule.Effective(Ivanov().Rates, Dates.Utc(year, month, day));

        Assert.NotNull(rate);
        Assert.Equal((decimal)expected, rate!.Value);
    }

    [Fact]
    public void До_первой_ставки_ставки_нет()
    {
        var rate = RateSchedule.Effective(Ivanov().Rates, Dates.Utc(2025, 12, 31));

        Assert.Null(rate);
    }

    [Fact]
    public void Без_ставки_на_дату_запись_создать_нельзя()
    {
        // Приёмочный сценарий 1: Петрова, 15.01.2026 — ставка у неё только с 01.02.2026.
        var petrova = new Employee
        {
            FullName = "Петрова А. С.",
            Rates = new List<EmployeeRate> { new() { From = Dates.Utc(2026, 2, 1), Value = 700m } }
        };

        var exception = Assert.Throws<BusinessRuleException>(
            () => RateSchedule.RequireEffectiveValue(petrova, Dates.Utc(2026, 1, 15)));

        Assert.Equal(ErrorCodes.RateNotDefined, exception.Code);
        Assert.Contains("15.01.2026", exception.Message);
        Assert.Contains("Петрова", exception.Message);
    }

    [Fact]
    public void Изменение_ставки_задним_числом_меняет_стоимость()
    {
        // Приёмочный сценарий 8: ставку с 01.03.2026 меняют на 650 ₽,
        // запись от 05.03.2026 на 8 часов должна стоить 5 200 ₽.
        var employee = Ivanov();
        var march = employee.Rates.Single(rate => rate.From == Dates.Utc(2026, 3, 1));
        march.Value = 650m;

        var value = RateSchedule.RequireEffectiveValue(employee, Dates.Utc(2026, 3, 5));

        Assert.Equal(650m, value);
        Assert.Equal(5200m, Money.Cost(8m, value));
    }

    [Fact]
    public void Пустая_история_ставок_не_роняет_выбор()
    {
        Assert.Null(RateSchedule.Effective(new List<EmployeeRate>(), Dates.Utc(2026, 3, 5)));
        Assert.Null(RateSchedule.Effective(null, Dates.Utc(2026, 3, 5)));
    }
}
