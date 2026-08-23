using Timesheet.Api.Domain;

namespace Timesheet.Api.Validation;

public static class PeriodValidator
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 20;

    public static void ValidateMonth(int year, int month)
    {
        var errors = new List<ValidationError>();

        if (year < MinYear || year > MaxYear)
        {
            errors.Add(new ValidationError("year", $"Год должен быть в диапазоне {MinYear}–{MaxYear}"));
        }

        if (month < 1 || month > 12)
        {
            errors.Add(new ValidationError("month", "Месяц должен быть числом от 1 до 12"));
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    /// <summary>Нормализуем пагинацию: страница с нуля или pageSize = 100000 не должны валить запрос.</summary>
    public static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1 ? DefaultPageSize : pageSize;

        if (normalizedPageSize > MaxPageSize)
        {
            throw new RequestValidationException(
                "pageSize",
                $"Размер страницы не может быть больше {MaxPageSize}");
        }

        return (normalizedPage, normalizedPageSize);
    }
}
