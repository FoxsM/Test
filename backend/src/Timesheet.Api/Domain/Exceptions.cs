namespace Timesheet.Api.Domain;

/// <summary>
/// Нарушение бизнес-правила. Долетает до middleware и превращается
/// в 400/409 с кодом и человекочитаемым текстом.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(
        string code,
        string message,
        int statusCode = StatusCodes.Status409Conflict,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }

    public string Code { get; }

    public int StatusCode { get; }

    /// <summary>Машиночитаемые подробности: лимиты, фактические значения и т. п.</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; }
}

/// <summary>Ошибка входных данных — формат, кратность, обязательность. Всегда 400.</summary>
public class RequestValidationException : Exception
{
    public RequestValidationException(IReadOnlyList<ValidationError> errors)
        : base("Запрос не прошёл валидацию")
    {
        Errors = errors;
    }

    public RequestValidationException(string field, string message)
        : this(new[] { new ValidationError(field, message) })
    {
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}

public record ValidationError(string Field, string Message);

/// <summary>Объект не найден. 404.</summary>
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message)
    {
    }
}
