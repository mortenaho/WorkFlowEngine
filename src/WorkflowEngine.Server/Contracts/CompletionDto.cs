namespace WorkflowEngine.Server;

public sealed class CompletionDto
{
    public string InstanceId { get; set; } = "";
    public bool AllCompleted { get; set; }
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Open { get; set; }
    public List<TaskDto> Tasks { get; set; } = [];
}
