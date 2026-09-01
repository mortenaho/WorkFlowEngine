namespace TaskFlow.Application;

public sealed class AssignToInput
{
    public string DefinitionKey { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string Title { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
    public string ToKind { get; set; } = "";
    public string ToId { get; set; } = "";
    public IReadOnlyList<string>? ToIds { get; set; }
    /// <summary>Join mode for parallel assignments. Use <see cref="JoinMode.All"/> when <see cref="OnAllCompleted"/> is set.</summary>
    public string Join { get; set; } = "";
    /// <summary>Next assignment created automatically when all parallel tasks are completed.</summary>
    public AssignToInput? OnAllCompleted { get; set; }
}
