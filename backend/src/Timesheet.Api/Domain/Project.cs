using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Timesheet.Api.Domain;

/// <summary>Проект. Шифр уникален (уникальный индекс по code).</summary>
public class Project
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>Шифр, например П-001.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Бюджет в рублях.</summary>
    public decimal Budget { get; set; }

    public DateTime StartDate { get; set; }

    /// <summary>null — проект бессрочный.</summary>
    public DateTime? EndDate { get; set; }
}
