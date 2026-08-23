using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Timesheet.Api.Domain;

/// <summary>
/// Сотрудник. История ставок хранится внутри документа: ставок мало,
/// они всегда нужны вместе с сотрудником и никогда не запрашиваются отдельно.
/// </summary>
public class Employee
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Ставка действует с даты <see cref="EmployeeRate.From"/> и до начала следующей.
    /// Порядок в массиве не гарантируется — выбор ставки всегда идёт через <see cref="RateSchedule"/>.
    /// </summary>
    public List<EmployeeRate> Rates { get; set; } = new();
}

public class EmployeeRate
{
    /// <summary>Дата (UTC, полночь), с которой действует ставка.</summary>
    public DateTime From { get; set; }

    /// <summary>Рублей в час. Деньги — только decimal (Decimal128 в Mongo).</summary>
    public decimal Value { get; set; }
}
