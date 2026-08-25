namespace WorkflowEngine.Server;

public sealed class CompleteRequest
{
    public string From { get; set; } = "";
    public string Note { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
}
