namespace TaskFlow.Application;

public sealed class CompleteResult
{
    public WorkflowTask Task { get; set; } = new();
    public Completion Completion { get; set; } = new();
    /// <summary>Populated when a parallel join auto-advances to the next assignment.</summary>
    public AssignToResult? Next { get; set; }
}
