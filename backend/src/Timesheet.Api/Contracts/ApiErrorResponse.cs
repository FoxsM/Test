namespace Timesheet.Api.Contracts;

/// <summary>
/// Единый формат ошибки для всех эндпоинтов: код для кода, текст для человека.
/// Голого текста исключения и 500 на бизнес-правилах быть не должно.
/// </summary>
public class ApiErrorResponse
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Пофайловые ошибки валидации: поле → сообщение.</summary>
    public List<ApiValidationError>? Errors { get; set; }

    /// <summary>Машиночитаемые подробности (лимиты, фактические значения).</summary>
    public Dictionary<string, object?>? Details { get; set; }

    /// <summary>Идентификатор запроса — по нему ошибку можно найти в логах.</summary>
    public string? TraceId { get; set; }
}

public class ApiValidationError
{
    public string Field { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
