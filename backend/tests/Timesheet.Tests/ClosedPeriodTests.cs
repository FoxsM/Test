using Timesheet.Api.Domain;
using Xunit;

namespace Timesheet.Tests;

/// <summary>Правило 4: в закрытом периоде записи нельзя создавать, изменять и удалять.</summary>
public class ClosedPeriodTests
{
    [Fact]
    public void В_закрытом_периоде_изменение_запрещено()
    {
        // Приёмочный сценарий 5: закрыли февраль 2026, пробуем тронуть запись от 20.02.2026.
        var exception = Assert.Throws<BusinessRuleException>(
            () => TimeEntryRules.EnsurePeriodOpen(isClosed: true, Dates.Utc(2026, 2, 20)));

        Assert.Equal(ErrorCodes.PeriodClosed, exception.Code);
        Assert.Equal(409, exception.StatusCode);
        Assert.Contains("02.2026", exception.Message);
        Assert.Equal(2026, exception.Details!["year"]);
        Assert.Equal(2, exception.Details!["month"]);
    }

    [Fact]
    public void В_открытом_периоде_изменение_разрешено()
    {
        var exception = Record.Exception(
            () => TimeEntryRules.EnsurePeriodOpen(isClosed: false, Dates.Utc(2026, 3, 5)));

        Assert.Null(exception);
    }
}
