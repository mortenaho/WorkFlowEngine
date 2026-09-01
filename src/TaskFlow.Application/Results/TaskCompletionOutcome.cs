namespace TaskFlow.Application;

public enum TaskCompletionStatus
{
    Approved,
    WaitingForOthers,
    AllDone,
}

/// <summary>
/// UI-friendly view of <see cref="CompleteResult"/> after <c>CompleteTask</c>.
/// Use when the client needs an immediate status without inspecting <c>Next</c> manually.
/// </summary>
public sealed class TaskCompletionOutcome
{
    public TaskCompletionStatus Status { get; init; }
    public CompleteResult Complete { get; init; } = new();
    public AssignToResult? Next { get; init; }

    /// <summary>Snake-case status for JSON APIs: <c>approved</c>, <c>waiting_for_others</c>, <c>all_done</c>.</summary>
    public string StatusKey => Status switch
    {
        TaskCompletionStatus.WaitingForOthers => "waiting_for_others",
        TaskCompletionStatus.AllDone => "all_done",
        _ => "approved",
    };

    public static TaskCompletionOutcome From(CompleteResult result)
    {
        if (result.Next is not null)
        {
            return new TaskCompletionOutcome
            {
                Status = TaskCompletionStatus.AllDone,
                Complete = result,
                Next = result.Next,
            };
        }

        if (!result.Completion.AllCompleted)
        {
            return new TaskCompletionOutcome
            {
                Status = TaskCompletionStatus.WaitingForOthers,
                Complete = result,
            };
        }

        return new TaskCompletionOutcome
        {
            Status = TaskCompletionStatus.Approved,
            Complete = result,
        };
    }
}
