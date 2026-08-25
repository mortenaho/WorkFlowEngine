namespace WorkflowEngine.Application;

public sealed class CompleteAndEndResult
{
    public WorkflowTask Task { get; set; } = new();
    public Completion Completion { get; set; } = new();
    public ProcessInstanceDetail Process { get; set; } = new();
    public int CancelledTasks { get; set; }
}
