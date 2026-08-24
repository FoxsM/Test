using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Domain;

namespace Timesheet.Api.Middleware;

/// <summary>
/// Единственное место, где исключения превращаются в HTTP-ответ.
/// Контроллеры не ловят ничего: нарушение бизнес-правила — это 400/409 с кодом,
/// а 500 остаётся только за настоящими сбоями, и наружу из него не уходит текст исключения.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (RequestValidationException exception)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, new ApiErrorResponse
            {
                Code = ErrorCodes.ValidationFailed,
                Message = "Проверьте заполненные поля",
                Errors = exception.Errors
                    .Select(error => new ApiValidationError
                    {
                        Field = Camel(error.Field),
                        Message = error.Message
                    })
                    .ToList()
            });
        }
        catch (BusinessRuleException exception)
        {
            _logger.LogInformation(
                "Бизнес-правило {Code}: {Message}",
                exception.Code,
                exception.Message);

            await WriteAsync(context, exception.StatusCode, new ApiErrorResponse
            {
                Code = exception.Code,
                Message = exception.Message,
                Details = exception.Details is null
                    ? null
                    : new Dictionary<string, object?>(exception.Details)
            });
        }
        catch (EntityNotFoundException exception)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, new ApiErrorResponse
            {
                Code = ErrorCodes.NotFound,
                Message = exception.Message
            });
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning(exception, "Нарушение уникального индекса");

            await WriteAsync(context, StatusCodes.Status409Conflict, new ApiErrorResponse
            {
                Code = ErrorCodes.DuplicateProjectCode,
                Message = "Такая запись уже существует: нарушено ограничение уникальности."
            });
        }
        catch (BadHttpRequestException exception)
        {
            // Сюда попадает нечитаемый JSON и неверные типы в теле запроса.
            _logger.LogInformation(exception, "Некорректное тело запроса");

            await WriteAsync(context, StatusCodes.Status400BadRequest, new ApiErrorResponse
            {
                Code = ErrorCodes.MalformedRequest,
                Message = "Тело запроса не разобрано: проверьте формат JSON и типы полей."
            });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Клиент закрыл вкладку — это не ошибка сервера, отвечать уже некому.
            _logger.LogDebug("Запрос {Path} отменён клиентом", context.Request.Path);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Необработанная ошибка при обработке {Path}", context.Request.Path);

            await WriteAsync(context, StatusCodes.Status500InternalServerError, new ApiErrorResponse
            {
                Code = ErrorCodes.InternalError,
                Message = "Внутренняя ошибка сервера. Попробуйте повторить операцию позже."
            });
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, ApiErrorResponse payload)
    {
        if (context.Response.HasStarted)
        {
            // Ответ уже частично ушёл клиенту — переписать статус нельзя.
            return;
        }

        payload.TraceId = context.TraceIdentifier;

        // Намеренно не вызываем Response.Clear(): он снёс бы заголовки CORS,
        // выставленные до нас, и браузер не смог бы прочитать текст ошибки.
        context.Response.ContentLength = null;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(payload, context.RequestAborted);
    }

    private static string Camel(string field) =>
        string.IsNullOrEmpty(field) ? field : char.ToLowerInvariant(field[0]) + field[1..];
}
