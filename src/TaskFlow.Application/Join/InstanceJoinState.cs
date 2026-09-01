using System.Text.Json;
using TaskFlow.Domain;

namespace TaskFlow.Application;

internal static class InstanceJoinState
{
    public const string Key = "__taskflow_join";

    public sealed record State(string Mode, bool Advanced, string Referrer, AssignToInput OnAllCompleted);

    public static void Attach(ProcessInstance inst, string referrer, string joinMode, AssignToInput continuation)
    {
        inst.Parameters ??= [];
        inst.Parameters[Key] = new Dictionary<string, object?>
        {
            ["mode"] = joinMode,
            ["advanced"] = false,
            ["referrer"] = referrer,
            ["onAllCompleted"] = AssignToInputSerializer.ToDictionary(continuation),
        };
    }

    public static State? Read(ProcessInstance inst)
    {
        if (inst.Parameters is null || !inst.Parameters.TryGetValue(Key, out var raw) || raw is null)
            return null;

        var bag = raw switch
        {
            Dictionary<string, object?> dict => dict,
            JsonElement { ValueKind: JsonValueKind.Object } el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText()) ?? [],
            _ => null,
        };
        if (bag is null)
            return null;

        var mode = bag.TryGetValue("mode", out var modeRaw) ? modeRaw?.ToString() ?? "" : "";
        var advanced = bag.TryGetValue("advanced", out var advRaw) && advRaw is true;
        var referrer = bag.TryGetValue("referrer", out var refRaw) ? refRaw?.ToString() ?? "" : "";
        if (mode.Length == 0 || referrer.Length == 0 || !bag.TryGetValue("onAllCompleted", out var contRaw))
            return null;

        return new State(mode, advanced, referrer, AssignToInputSerializer.FromObject(contRaw));
    }

    public static void MarkAdvanced(ProcessInstance inst)
    {
        if (inst.Parameters is null || !inst.Parameters.TryGetValue(Key, out var raw) || raw is null)
            return;

        var bag = raw switch
        {
            Dictionary<string, object?> dict => dict,
            JsonElement { ValueKind: JsonValueKind.Object } el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText()) ?? [],
            _ => null,
        };
        if (bag is null)
            return;

        bag["advanced"] = true;
        inst.Parameters[Key] = bag;
    }
}
