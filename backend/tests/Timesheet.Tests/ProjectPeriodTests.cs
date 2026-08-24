using Timesheet.Api.Domain;
using Xunit;

namespace Timesheet.Tests;

/// <summary>Правило 5: дата записи не раньше начала проекта и не позже окончания, если оно задано.</summary>
public class ProjectPeriodTests
{
    private static Project Project001() => new()
    {
        Code = "П-001",
        Name = "Реконструкция цеха",
        Budget = 20000m,
        StartDate = Dates.Utc(2026, 1, 1),
        EndDate = Dates.Utc(2026, 3, 31)
    };

    private static Project Project002() => new()
    {
        Code = "П-002",
        Name = "Инженерные сети",
        Budget = 5000m,
        StartDate = Dates.Utc(2026, 3, 1),
        EndDate = null
    };

    [Theory]
    [InlineData(2026, 1, 1)]   // первый день проекта
    [InlineData(2026, 2, 20)]
    [InlineData(2026, 3, 31)]  // последний день проекта — граница включительно
    public void Дата_внутри_периода_проходит(int year, int month, int day)
    {
        var exception = Record.Exception(
            () => TimeEntryRules.EnsureWithinProjectPeriod(Project001(), Dates.Utc(year, month, day)));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(2025, 12, 31)] // раньше начала
    [InlineData(2026, 4, 1)]   // позже окончания
    public void Дата_вне_периода_отклоняется(int year, int month, int day)
    {
        var exception = Assert.Throws<BusinessRuleException>(
            () => TimeEntryRules.EnsureWithinProjectPeriod(Project001(), Dates.Utc(year, month, day)));

        Assert.Equal(ErrorCodes.DateOutOfProjectPeriod, exception.Code);
        Assert.Contains("П-001", exception.Message);
    }

    [Fact]
    public void Запись_раньше_начала_бессрочного_проекта_отклоняется()
    {
        // Приёмочный сценарий 4: П-002 начинается 01.03.2026, запись датой 20.02.2026.
        var exception = Assert.Throws<BusinessRuleException>(
            () => TimeEntryRules.EnsureWithinProjectPeriod(Project002(), Dates.Utc(2026, 2, 20)));

        Assert.Equal(ErrorCodes.DateOutOfProjectPeriod, exception.Code);
        Assert.Contains("20.02.2026", exception.Message);
        Assert.Contains("без даты окончания", exception.Message);
    }

    [Fact]
    public void У_бессрочного_проекта_нет_верхней_границы()
    {
        var exception = Record.Exception(
            () => TimeEntryRules.EnsureWithinProjectPeriod(Project002(), Dates.Utc(2030, 12, 31)));

        Assert.Null(exception);
    }
}
