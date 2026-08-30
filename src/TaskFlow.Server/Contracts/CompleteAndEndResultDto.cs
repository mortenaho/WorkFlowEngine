namespace TaskFlow.Server;

public sealed class CompleteAndEndResultDto
{
    public TaskDto Task { get; set; } = new();
    public CompletionDto Completion { get; set; } = new();
    public ProcessInstanceDetailDto Process { get; set; } = new();
    public int CancelledTasks { get; set; }
}
