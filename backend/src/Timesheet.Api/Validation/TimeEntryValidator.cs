using System.Globalization;
using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;
using Timesheet.Api.Infrastructure;

namespace Timesheet.Api.Validation;

/// <summary>
/// Валидация входных данных: формат, обязательность, диапазоны, кратность.
/// Здесь нет ни одного обращения к базе — всё, что требует данных
/// (ставка на дату, лимит за день, закрытый период, период проекта),
/// живёт в TimeEntryService и даёт 409, а не 400.
/// </summary>
public static class TimeEntryValidator
{
    public const int MaxCommentLength = 1000;

    public static void Validate(SaveTimeEntryRequest? request, bool requireVersion)
    {
        if (request is null)
        {
            throw new RequestValidationException("body", "Пустое тело запроса");
        }

        var errors = new List<ValidationError>();

        if (!Ids.IsValid(request.EmployeeId))
        {
            errors.Add(new ValidationError(nameof(request.EmployeeId), "Выберите сотрудника"));
        }

        if (!Ids.IsValid(request.ProjectId))
        {
            errors.Add(new ValidationError(nameof(request.ProjectId), "Выберите проект"));
        }

        if (request.Date is null)
        {
            errors.Add(new ValidationError(nameof(request.Date), "Укажите дату в формате ГГГГ-ММ-ДД"));
        }

        ValidateHours(request.Hours, errors);

        if (request.Comment is { Length: > MaxCommentLength })
        {
            errors.Add(new ValidationError(
                nameof(request.Comment),
                $"Комментарий длиннее {MaxCommentLength} символов"));
        }

        if (requireVersion)
        {
            if (request.Version is null)
            {
                errors.Add(new ValidationError(
                    nameof(request.Version),
                    "Не передана версия записи — без неё нельзя проверить, что запись не изменил кто-то другой"));
            }
            else if (request.Version < 1)
            {
                errors.Add(new ValidationError(nameof(request.Version), "Версия записи должна быть положительной"));
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static void ValidateHours(decimal? hours, List<ValidationError> errors)
    {
        if (hours is null)
        {
            errors.Add(new ValidationError(nameof(SaveTimeEntryRequest.Hours), "Укажите количество часов"));
            return;
        }

        var value = hours.Value;

        if (!WorkTimeRules.IsPositive(value))
        {
            errors.Add(new ValidationError(nameof(SaveTimeEntryRequest.Hours), "Часы должны быть больше нуля"));
            return;
        }

        if (!WorkTimeRules.FitsSingleEntry(value))
        {
            errors.Add(new ValidationError(
                nameof(SaveTimeEntryRequest.Hours),
                $"В одной записи не может быть больше {Format(WorkTimeRules.MaxHoursPerEntry)} ч"));
        }

        if (!WorkTimeRules.IsMultipleOfStep(value))
        {
            errors.Add(new ValidationError(
                nameof(SaveTimeEntryRequest.Hours),
                $"Часы задаются с шагом {Format(WorkTimeRules.HoursStep)} ч: например 0,5 или 7,5"));
        }
    }

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
}
