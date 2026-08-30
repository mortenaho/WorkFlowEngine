namespace TaskFlow.Server;

public sealed class TaskDto
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string AssigneeKind { get; set; } = "";
    public string AssigneeId { get; set; } = "";
    public string AssignedBy { get; set; } = "";
    public string ClaimedBy { get; set; } = "";
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
