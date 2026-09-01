using System.Text.Json;

namespace TaskFlow.Application;

internal static class AssignToInputSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static Dictionary<string, object?> ToDictionary(AssignToInput input)
    {
        var json = JsonSerializer.Serialize(input, Options);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, Options) ?? [];
    }

    public static AssignToInput FromObject(object? raw)
    {
        if (raw is null)
            return new AssignToInput();

        var json = raw switch
        {
            JsonElement el => el.GetRawText(),
            Dictionary<string, object?> dict => JsonSerializer.Serialize(dict, Options),
            _ => JsonSerializer.Serialize(raw, Options),
        };
        return JsonSerializer.Deserialize<AssignToInput>(json, Options) ?? new AssignToInput();
    }
}
