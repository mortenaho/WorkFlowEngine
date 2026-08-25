namespace WorkflowEngine.Domain;

public sealed class ProcessInstance
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string Status { get; set; } = InstanceStatus.Running;
    public Dictionary<string, object?>? Parameters { get; set; }
    public string StartedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ProcessInstance Clone() => new()
    {
        Id = Id,
        TenantId = TenantId,
        DefinitionId = DefinitionId,
        DefinitionKey = DefinitionKey,
        ParentInstanceId = ParentInstanceId,
        Status = Status,
        Parameters = Vars.Clone(Parameters),
        StartedBy = StartedBy,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
    };
}
