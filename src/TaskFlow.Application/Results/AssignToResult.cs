namespace TaskFlow.Application;

public sealed class AssignToResult
{
    public string InstanceId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public WorkflowTask? Task { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = [];
}
