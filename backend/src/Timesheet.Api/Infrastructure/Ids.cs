using MongoDB.Bson;
using Timesheet.Api.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Идентификаторы наружу — строки, внутри — ObjectId.
/// Кривой id из query string должен давать 400 с понятным текстом, а не 500 из ObjectId.Parse.
/// </summary>
public static class Ids
{
    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && ObjectId.TryParse(value, out _);

    public static ObjectId Parse(string? value, string field)
    {
        if (!IsValid(value))
        {
            throw new RequestValidationException(field, $"Некорректный идентификатор: «{value}»");
        }

        return ObjectId.Parse(value);
    }

    public static ObjectId? ParseOptional(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : Parse(value, field);
}
