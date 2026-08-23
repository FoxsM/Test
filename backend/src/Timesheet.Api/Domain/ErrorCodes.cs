namespace Timesheet.Api.Domain;

/// <summary>
/// Машиночитаемые коды ошибок. Фронтенд различает ситуации по коду,
/// пользователю показывается message.
///
/// Разделение статусов:
///   400 — проблема в самом запросе (формат, кратность, диапазон);
///   409 — запрос корректен, но конфликтует с текущим состоянием данных
///         (закрытый период, лимит дня, чужие изменения, отсутствие ставки).
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string NotFound = "NOT_FOUND";
    public const string RateNotDefined = "RATE_NOT_DEFINED";
    public const string DailyHoursLimitExceeded = "DAILY_HOURS_LIMIT_EXCEEDED";
    public const string PeriodClosed = "PERIOD_CLOSED";
    public const string DateOutOfProjectPeriod = "DATE_OUT_OF_PROJECT_PERIOD";
    public const string ConcurrentModification = "CONCURRENT_MODIFICATION";
    public const string DuplicateProjectCode = "DUPLICATE_PROJECT_CODE";
    public const string InternalError = "INTERNAL_ERROR";
}
