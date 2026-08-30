namespace TaskFlow.Application;

public static class TenantContext
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string Id => Tenant.Normalize(CurrentValue.Value);

    public static IDisposable Use(string? tenantId)
    {
        var previous = CurrentValue.Value;
        CurrentValue.Value = Tenant.Normalize(tenantId);
        return new Pop(previous);
    }

    private sealed class Pop(string? previous) : IDisposable
    {
        public void Dispose() => CurrentValue.Value = previous;
    }
}
