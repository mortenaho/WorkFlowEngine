namespace TaskFlow.Application;

public enum TaskCompletionStatus
{
    Approved,
    WaitingForOthers,
    AllDone,
}

public sealed class TaskCompletionOutcome
{
    public TaskCompletionStatus Status { get; init; }
    public CompleteResult Complete { get; init; } = new();
    public AssignToResult? Next { get; init; }

    public string StatusKey => Status switch
    {
        TaskCompletionStatus.WaitingForOthers => "waiting_for_others",
        TaskCompletionStatus.AllDone => "all_done",
        _ => "approved",
    };

    public static TaskCompletionOutcome From(CompleteResult result)
    {
        if (result.Next is not null)
            return new() { Status = TaskCompletionStatus.AllDone, Complete = result, Next = result.Next };

        if (!result.Completion.AllCompleted)
            return new() { Status = TaskCompletionStatus.WaitingForOthers, Complete = result };

        return new() { Status = TaskCompletionStatus.Approved, Complete = result };
    }
}
