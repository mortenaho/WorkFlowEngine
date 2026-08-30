namespace TaskFlow.Application;

public sealed class ReferResult
{
    public string InstanceId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public WorkflowTask? Task { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = [];
}
