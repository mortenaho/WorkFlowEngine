namespace WorkflowEngine.Application;

public sealed class TaskFilter
{
    public string? UserId { get; set; }
    public string? GroupId { get; set; }
    public IReadOnlyList<string>? GroupIds { get; set; }
    public string? InstanceId { get; set; }
    public string? Status { get; set; }
    public IReadOnlyList<string>? Statuses { get; set; }
    public string? ClaimedBy { get; set; }
    public string? TenantId { get; set; }
}
