using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TaskFlow.Client;

public static class ClientJson
{
    public static readonly JsonSerializerOptions Options = Create();

    public static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { OmitEmptyValues },
            },
        };
        return o;
    }

    private static void OmitEmptyValues(JsonTypeInfo info)
    {
        foreach (var prop in info.Properties)
        {
            if (prop.PropertyType == typeof(string))
                prop.ShouldSerialize = (_, val) => !string.IsNullOrEmpty(val as string);
            else if (prop.PropertyType == typeof(Dictionary<string, object?>))
                prop.ShouldSerialize = (_, val) => val is Dictionary<string, object?> { Count: > 0 };
        }
    }
}
