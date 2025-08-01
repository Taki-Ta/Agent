
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class FunctionCallConverter : JsonConverter<FunctionCall>
{
    public override FunctionCall Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 可按需实现反序列化
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var name = root.TryGetProperty("name", out var argNameProp) && argNameProp.ValueKind != JsonValueKind.Null
            ? argNameProp.GetString()
            : "";
        var arguments = root.TryGetProperty("arguments", out var argProp) && argProp.ValueKind != JsonValueKind.Null
            ? argProp.GetString()
            : "{}";

        return new FunctionCall(name, arguments);
    }

    public override void Write(Utf8JsonWriter writer, FunctionCall value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.name);

        if (value.arguments == null)
        {
            writer.WriteRawValue("\"{}\"");
        }
        else
        {
            writer.WriteString("arguments", value.arguments);
        }

        writer.WriteEndObject();
    }
}
