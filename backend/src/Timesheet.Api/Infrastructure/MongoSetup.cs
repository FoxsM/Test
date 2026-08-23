using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Глобальные настройки маппинга. Вызывается один раз до первой сериализации —
/// первой строкой Program.cs.
/// </summary>
public static class MongoSetup
{
    private static readonly object Sync = new();
    private static bool _configured;

    public static void Configure()
    {
        lock (Sync)
        {
            if (_configured)
            {
                return;
            }

            _configured = true;

            ConventionRegistry.Register(
                "timesheet",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true)
                },
                _ => true);

            // По умолчанию драйвер пишет decimal строкой. Строку нельзя ни просуммировать
            // в $group, ни сравнить в $gt, поэтому явно переводим деньги в Decimal128.
            BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.RegisterSerializer(
                typeof(decimal?),
                new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

            // BSON-даты всегда UTC. Фиксируем Kind, чтобы из базы не приезжали Unspecified,
            // которые потом молча сдвинутся на часовой пояс сервера.
            BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Utc));
            BsonSerializer.RegisterSerializer(
                typeof(DateTime?),
                new NullableSerializer<DateTime>(new DateTimeSerializer(DateTimeKind.Utc)));
        }
    }
}
