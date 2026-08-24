using Timesheet.Api.Domain;
using Xunit;

namespace Timesheet.Tests;

/// <summary>
/// Правило 7: деньги — decimal, округление до копеек.
/// Режим округления — ToEven, тот же, что у оператора $round в агрегации MongoDB.
/// </summary>
public class MoneyTests
{
    [Theory]
    [InlineData(8, 500, 4000)]     // приёмочная проверка: 20.02.2026
    [InlineData(8, 600, 4800)]     // 05.03.2026, ставка уже 600
    [InlineData(4, 700, 2800)]
    [InlineData(10, 700, 7000)]
    [InlineData(8, 650, 5200)]     // сценарий 8: ставка задним числом стала 650
    [InlineData(0.5, 333, 166.5)]
    public void Стоимость_записи_это_часы_на_ставку(double hours, double rate, double expected)
    {
        var amount = Money.Cost((decimal)hours, (decimal)rate);

        Assert.Equal((decimal)expected, amount);
    }

    [Fact]
    public void Округление_до_копеек_идёт_к_чётному_как_в_Mongo()
    {
        // 0,5 × 333,33 = 166,665 → 166,66 (к чётному), а не 166,67.
        Assert.Equal(166.66m, Money.Cost(0.5m, 333.33m));

        // 0,5 × 333,35 = 166,675 → 166,68.
        Assert.Equal(166.68m, Money.Cost(0.5m, 333.35m));
    }

    [Fact]
    public void Копейки_не_теряются_на_дробных_ставках()
    {
        Assert.Equal(1234.57m, Money.Cost(7.5m, 164.6094m));
    }

    [Fact]
    public void Decimal_не_накапливает_ошибку_как_double()
    {
        // Тот же расчёт на double дал бы 0.30000000000000004.
        var sum = Money.Cost(0.5m, 0.2m) + Money.Cost(0.5m, 0.4m);

        Assert.Equal(0.30m, sum);
    }
}
