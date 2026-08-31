namespace TaskFlow.Application;

public sealed class CompleteAndAssignToResult
{
    public CompleteResult Complete { get; set; } = new();
    public AssignToResult? Next { get; set; }
}
