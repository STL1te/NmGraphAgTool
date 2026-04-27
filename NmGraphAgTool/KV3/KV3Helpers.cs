using System;
using System.Collections.Generic;
using System.IO;
using ValveKeyValue;
using ValveKeyValue.KeyValues3;

namespace NmGraphAgTool.KV3;

internal static class KV3Helpers
{
    private readonly static KV3ID TextEncoding = new("text", new Guid("e21c7f3c-8a33-41c5-9977-a76d3a32aa0d"));
    private readonly static KV3ID GenericFormat = new("generic", new Guid("7412167c-06e9-4698-aff2-e63eb59037e7"));

    public static KVDocument ToKV3Document(this KVObject root)
        => new(
            new KVHeader
            {
                Encoding = TextEncoding,
                Format = GenericFormat,
            },
            null,
            root);

    public static string ToKV3String(this KVDocument document)
    {
        using var memoryStream = new MemoryStream();
        var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues3Text);
        serializer.Serialize(memoryStream, document);
        memoryStream.Position = 0;

        using var reader = new StreamReader(memoryStream);
        return reader.ReadToEnd();
    }

    public static string ToKV3String(this KVObject root)
        => root.ToKV3Document().ToKV3String();

    public static KVDocument ParseKV3(Stream stream)
    {
        var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues3Text);
        return serializer.Deserialize(stream);
    }

    public static KVDocument ParseKV3(string path)
    {
        using var stream = File.OpenRead(path);
        return ParseKV3(stream);
    }

    public static string GetStringProperty(this KVObject obj, string name, string? defaultValue = null)
    {
        if (!obj.TryGetValue(name, out var value) || value.ValueType != KVValueType.String)
        {
            return defaultValue!;
        }

        return (string)value;
    }

    public static IReadOnlyList<KVObject> GetArray(this KVObject obj, string name)
    {
        if (!obj.TryGetValue(name, out var value) || !value.IsArray)
        {
            return null!;
        }

        return (IReadOnlyList<KVObject>)value.Values;
    }
}
