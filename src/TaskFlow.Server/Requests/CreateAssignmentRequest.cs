namespace TaskFlow.Server;

public sealed class CreateAssignmentRequest
{
    public string DefinitionKey { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string From { get; set; } = "";
    public string Title { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
    public string Join { get; set; } = "";
    public CreateAssignmentRequest? OnAllCompleted { get; set; }
    public AssigneeRequest To { get; set; } = new();
}
