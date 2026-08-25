namespace WorkflowEngine.Server;

public sealed class ReferResultDto
{
    public string InstanceId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public TaskDto? Task { get; set; }
    public List<TaskDto> Tasks { get; set; } = [];
}
