namespace WorkflowEngine.Application;

public sealed class CompleteResult
{
    public WorkflowTask Task { get; set; } = new();
    public Completion Completion { get; set; } = new();
}
