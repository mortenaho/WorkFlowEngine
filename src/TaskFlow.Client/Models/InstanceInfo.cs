namespace TaskFlow.Client;

/// <summary>Wire shape of GET /v1/instances/{id} (Initiator maps from StartedBy on the server).</summary>
public sealed class InstanceInfo
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string Status { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
    public string Initiator { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
