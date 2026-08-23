namespace Timesheet.Api.Domain;

/// <summary>
/// Деньги — только decimal. Округление до копеек.
///
/// Режим округления — ToEven (математическое «к чётному»), и это осознанный выбор:
/// оператор $round в агрегации MongoDB округляет именно так, а стоимость считается
/// в двух местах (список записей и отчёт). Разные режимы дали бы расхождение
/// в копейку между итогом на экране и итогом в отчёте. Если бухгалтерии нужен
/// half-up — менять надо оба места сразу, см. NOTES.md.
/// </summary>
public static class Money
{
    public const int Scale = 2;

    public static decimal Round(decimal value) => Math.Round(value, Scale, MidpointRounding.ToEven);

    /// <summary>Стоимость записи табеля: часы × ставка, округлённые до копеек.</summary>
    public static decimal Cost(decimal hours, decimal rate) => Round(hours * rate);
}
