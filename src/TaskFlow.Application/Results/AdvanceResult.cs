namespace TaskFlow.Application;

public sealed class AdvanceResult
{
    public CompleteResult Complete { get; set; } = new();
    public ReferResult? Next { get; set; }
}
