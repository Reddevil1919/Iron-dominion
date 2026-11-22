using System.Text.Json;
using System.Text.Json.Serialization;
using IronDominion.Domain;

namespace IronDominion.Infrastructure.Persistence;

internal sealed class EntityIdConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value) || value < 0)
            throw new JsonException("Invalid entity ID.");
        return value == 0 ? default : new EntityId(value);
    }

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal sealed class PlayerIdConverter : JsonConverter<PlayerId>
{
    public override PlayerId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value) || value < 0)
            throw new JsonException("Invalid player ID.");
        return value == 0 ? default : new PlayerId(value);
    }

    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal sealed class SimVectorConverter : JsonConverter<SimVector>
{
    public override SimVector Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var point = JsonSerializer.Deserialize<Point>(ref reader, options);
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Z))
            throw new JsonException("Invalid map position.");
        return new SimVector(point.X, point.Z);
    }

    public override void Write(Utf8JsonWriter writer, SimVector value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, new Point(value.X, value.Z), options);

    private readonly record struct Point(float X, float Z);
}
