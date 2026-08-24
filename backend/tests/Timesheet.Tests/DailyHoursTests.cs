using Timesheet.Api.Domain;
using Xunit;

namespace Timesheet.Tests;

/// <summary>
/// Правило 2: не больше 24 часов за календарный день по всем проектам.
/// Правило 3: больше 12 часов за день — запись сохраняется, день помечается переработкой.
/// </summary>
public class DailyHoursTests
{
    private static readonly Employee Ivanov = new() { FullName = "Иванов И. И." };
    private static readonly DateTime March6 = Dates.Utc(2026, 3, 6);

    [Theory]
    [InlineData(0, 8)]      // обычный день
    [InlineData(20, 4)]     // ровно 24 часа — предел, но не превышение
    [InlineData(23.5, 0.5)] // граница по шагу 0,5
    public void В_пределах_суток_запись_проходит(double alreadyLogged, double requested)
    {
        var exception = Record.Exception(() => TimeEntryRules.EnsureWithinDailyLimit(
            Ivanov,
            March6,
            (decimal)alreadyLogged,
            (decimal)requested));

        Assert.Null(exception);
    }

    [Fact]
    public void Больше_24_часов_за_день_отклоняется()
    {
        // Приёмочный сценарий 3: у Иванова за 06.03.2026 уже 20 часов, добавляем ещё 6.
        var exception = Assert.Throws<BusinessRuleException>(
            () => TimeEntryRules.EnsureWithinDailyLimit(Ivanov, March6, alreadyLogged: 20m, requested: 6m));

        Assert.Equal(ErrorCodes.DailyHoursLimitExceeded, exception.Code);
        Assert.Equal(409, exception.StatusCode);
        Assert.Contains("06.03.2026", exception.Message);
        Assert.Contains("26", exception.Message);
        Assert.NotNull(exception.Details);
        Assert.Equal(26m, exception.Details!["totalHours"]);
    }

    [Theory]
    [InlineData(12, false)]   // ровно 12 — ещё не переработка
    [InlineData(12.5, true)]
    [InlineData(20, true)]    // приёмочный сценарий 2
    [InlineData(24, true)]
    public void День_помечается_переработкой_после_12_часов(double dayHours, bool expected)
    {
        Assert.Equal(expected, WorkTimeRules.IsOvertime((decimal)dayHours));
    }

    [Fact]
    public void Переработка_не_мешает_сохранить_запись()
    {
        // Сценарий 2: 20 часов за день — это переработка, но не отказ.
        var exception = Record.Exception(() => TimeEntryRules.EnsureWithinDailyLimit(
            Ivanov,
            March6,
            alreadyLogged: 0m,
            requested: 20m));

        Assert.Null(exception);
        Assert.True(WorkTimeRules.IsOvertime(20m));
    }
}
