using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Timesheet.Api.Domain;

/// <summary>
/// Запись табеля. Ставка и стоимость намеренно НЕ хранятся в документе:
/// ставки можно менять задним числом, и отчёт обязан меняться вслед за ними
/// (приёмочный сценарий 8). Стоимость считается на чтении — см. NOTES.md.
/// </summary>
public class TimeEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string EmployeeId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Дата записи, UTC-полночь. Времени у записи табеля нет.</summary>
    public DateTime Date { get; set; }

    public decimal Hours { get; set; }

    public string? Comment { get; set; }

    /// <summary>Версия для оптимистичной блокировки. Растёт на каждом обновлении.</summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
