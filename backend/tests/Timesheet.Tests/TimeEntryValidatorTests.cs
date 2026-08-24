using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;
using Timesheet.Api.Validation;
using Xunit;

namespace Timesheet.Tests;

/// <summary>
/// Правило 6: часы положительные, кратные 0,5, не больше 24 за запись.
/// Это валидация запроса — она не ходит в базу и даёт 400, а не 409.
/// </summary>
public class TimeEntryValidatorTests
{
    private const string EmployeeId = "111111111111111111111111";
    private const string ProjectId = "aaaaaaaaaaaaaaaaaaaaaaaa";

    private static SaveTimeEntryRequest Request(decimal? hours) => new()
    {
        EmployeeId = EmployeeId,
        ProjectId = ProjectId,
        Date = new DateOnly(2026, 3, 5),
        Hours = hours
    };

    [Theory]
    [InlineData(0.5)]
    [InlineData(7.5)]
    [InlineData(8)]
    [InlineData(24)]
    public void Корректные_часы_проходят(double hours)
    {
        var exception = Record.Exception(
            () => TimeEntryValidator.Validate(Request((decimal)hours), requireVersion: false));

        Assert.Null(exception);
    }

    [Fact]
    public void Ноль_часов_это_ошибка_валидации()
    {
        // Приёмочный сценарий 6, первая половина.
        var exception = Assert.Throws<RequestValidationException>(
            () => TimeEntryValidator.Validate(Request(0m), requireVersion: false));

        Assert.Contains(exception.Errors, error => error.Field == nameof(SaveTimeEntryRequest.Hours));
        Assert.Contains(exception.Errors, error => error.Message.Contains("больше нуля"));
    }

    [Fact]
    public void Часы_не_кратные_получасу_это_ошибка_валидации()
    {
        // Приёмочный сценарий 6, вторая половина: 3,7 часа.
        var exception = Assert.Throws<RequestValidationException>(
            () => TimeEntryValidator.Validate(Request(3.7m), requireVersion: false));

        Assert.Contains(exception.Errors, error => error.Message.Contains("шагом"));
    }

    [Fact]
    public void Больше_24_часов_в_одной_записи_нельзя()
    {
        var exception = Assert.Throws<RequestValidationException>(
            () => TimeEntryValidator.Validate(Request(25m), requireVersion: false));

        Assert.Contains(exception.Errors, error => error.Message.Contains("24"));
    }

    [Fact]
    public void Ошибки_собираются_все_сразу()
    {
        var request = new SaveTimeEntryRequest
        {
            EmployeeId = "не-objectid",
            ProjectId = null,
            Date = null,
            Hours = -1m
        };

        var exception = Assert.Throws<RequestValidationException>(
            () => TimeEntryValidator.Validate(request, requireVersion: false));

        // Пользователь должен увидеть все проблемы формы за один заход, а не по одной.
        Assert.Equal(4, exception.Errors.Count);
    }

    [Fact]
    public void При_изменении_версия_обязательна()
    {
        // Без версии сохранение молча затёрло бы чужие правки — см. правило 8.
        var exception = Assert.Throws<RequestValidationException>(
            () => TimeEntryValidator.Validate(Request(8m), requireVersion: true));

        Assert.Contains(exception.Errors, error => error.Field == nameof(SaveTimeEntryRequest.Version));
    }
}
