namespace TaskFlow.Application;

public sealed class TaskCompletedEvent
{
    public required CompleteResult Complete { get; init; }
}
