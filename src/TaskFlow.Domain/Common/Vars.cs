namespace TaskFlow.Domain;

public static class Vars
{
    public static Dictionary<string, object?> Clone(Dictionary<string, object?>? v)
    {
        if (v is null || v.Count == 0)
            return [];
        return new Dictionary<string, object?>(v);
    }

    public static Dictionary<string, object?> Merge(Dictionary<string, object?>? v, Dictionary<string, object?>? other)
    {
        var outDict = Clone(v);
        if (other is null)
            return outDict;
        foreach (var (k, val) in other)
            outDict[k] = val;
        return outDict;
    }
}
