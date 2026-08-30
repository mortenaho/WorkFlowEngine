namespace TaskFlow.Server;

public sealed class CompleteResultDto
{
    public TaskDto Task { get; set; } = new();
    public CompletionDto Completion { get; set; } = new();
}
