namespace WorkflowEngine.Server;

public sealed class AssigneeRequest
{
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public List<string>? Ids { get; set; }
}
