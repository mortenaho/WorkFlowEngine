namespace TaskFlow.Domain;

public sealed class WorkflowTask
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
    public string Status { get; set; } = TaskStatus.Open;
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public WorkflowTask Clone()
    {
        var cp = new WorkflowTask
        {
            Id = Id,
            TenantId = TenantId,
            InstanceId = InstanceId,
            ParentInstanceId = ParentInstanceId,
            DefinitionKey = DefinitionKey,
            Title = Title,
            AssigneeKind = AssigneeKind,
            AssigneeId = AssigneeId,
            AssignedBy = AssignedBy,
            ClaimedBy = ClaimedBy,
            Status = Status,
            Note = Note,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
        if (CompletedAt is { } ts)
            cp.CompletedAt = ts;
        return cp;
    }
}
