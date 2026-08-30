namespace TaskFlow.Application;

public sealed class ReferInput
{
    public string DefinitionKey { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string Title { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
    public string ToKind { get; set; } = "";
    public string ToId { get; set; } = "";
    public IReadOnlyList<string>? ToIds { get; set; }
}
