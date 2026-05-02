using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOCCHI.Common.Data.Zones.Graph;

public class NodeMetadataConverter : JsonConverter<INodeMetadata>
{
    public override INodeMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("MetadataType", out var typeProp))
        {
            throw new JsonException("Missing MetadataType discriminator");
        }

        var typeName = typeProp.GetString();

        return typeName switch
        {
            nameof(BlankNodeMetadata) => JsonSerializer.Deserialize<BlankNodeMetadata>(root.GetRawText(), options)!,
            nameof(CarrotNodeMetaData) => JsonSerializer.Deserialize<CarrotNodeMetaData>(root.GetRawText(), options)!,
            nameof(RerollPotChestNodeMetaData) => JsonSerializer.Deserialize<RerollPotChestNodeMetaData>(root.GetRawText(), options)!,
            nameof(TeleportNodeMetadata) => JsonSerializer.Deserialize<TeleportNodeMetadata>(root.GetRawText(), options)!,
            nameof(ActivityNodeMetadata) => JsonSerializer.Deserialize<ActivityNodeMetadata>(root.GetRawText(), options)!,
            nameof(TreasureNodeMetaData) => JsonSerializer.Deserialize<TreasureNodeMetaData>(root.GetRawText(), options)!,
            nameof(PotChestNodeMetaData) => JsonSerializer.Deserialize<PotChestNodeMetaData>(root.GetRawText(), options)!,
            _ => throw new JsonException($"Unknown metadata type: {typeName}")
        };
    }

    public override void Write(Utf8JsonWriter writer, INodeMetadata value, JsonSerializerOptions options)
    {
        var typeName = value.GetType().Name;

        var json = JsonSerializer.SerializeToElement(value, value.GetType(), options);

        writer.WriteStartObject();
        writer.WriteString("MetadataType", typeName);

        foreach (var prop in json.EnumerateObject())
        {
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
