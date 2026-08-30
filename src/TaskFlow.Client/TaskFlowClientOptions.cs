namespace TaskFlow.Client;

public sealed class TaskFlowClientOptions
{
    /// <summary>Base URL of the already-running TaskFlow.Server, e.g. http://taskflow:8081/</summary>
    public Uri BaseAddress { get; set; } = new("http://127.0.0.1:8081/");

    /// <summary>Shared service key sent as X-API-Key (required outside Development on the server).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Default X-Tenant-Id when a call does not pass an explicit tenant.</summary>
    public string? TenantId { get; set; }
}
