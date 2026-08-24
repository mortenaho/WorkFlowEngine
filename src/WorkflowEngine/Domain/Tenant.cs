namespace WorkflowEngine;

public static class TenantContext
{
    public const string Default = "default";

    private static readonly AsyncLocal<string?> Current = new();

    public static string Id => Normalize(Current.Value);

    public static string Normalize(string? id) => string.IsNullOrEmpty(id) ? Default : id;

    public static IDisposable Use(string? tenantId)
    {
        var previous = Current.Value;
        Current.Value = Normalize(tenantId);
        return new Pop(previous);
    }

    private sealed class Pop(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
