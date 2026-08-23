using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Timesheet.Api.Domain;

/// <summary>Закрытый бухгалтерией месяц. Уникальный индекс по (year, month).</summary>
public class ClosedPeriod
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public int Year { get; set; }

    public int Month { get; set; }

    public DateTime ClosedAt { get; set; }

    public string ClosedBy { get; set; } = string.Empty;
}
