namespace WorkflowEngine.Application;

public sealed class ProcessInstanceDetail
{
    public string InstanceId { get; set; } = "";
    public string ProcessKey { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public string Initiator { get; set; } = "";
    public string Status { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = [];
    public int TaskTotal { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksOpen { get; set; }
    public bool AllTasksCompleted { get; set; }
}
