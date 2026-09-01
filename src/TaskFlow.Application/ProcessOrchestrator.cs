namespace TaskFlow.Application;

/// <summary>
/// Thin app-layer helper: complete a task and, when the assignment is fully done,
/// automatically create the next assignment. Keeps Engine assignment-based.
/// </summary>
public sealed class ProcessOrchestrator(Engine engine)
{
    public async Task<CompleteAndAssignToResult> CompleteAndAssignTo(
        string taskId,
        string actor,
        string note,
        Func<CompleteResult, AssignToInput?>? nextWhenAllCompleted = null,
        Dictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var done = await engine.CompleteTask(taskId, actor, note, parameters, cancellationToken);
        AssignToResult? next = done.Next;
        if (next is null && done.Completion.AllCompleted && nextWhenAllCompleted is not null)
        {
            var input = nextWhenAllCompleted(done);
            if (input is not null)
            {
                if (input.DefinitionKey.Length == 0)
                    input.DefinitionKey = done.Task.DefinitionKey;
                if (input.ParentInstanceId.Length == 0)
                {
                    input.ParentInstanceId = done.Task.ParentInstanceId.Length > 0
                        ? done.Task.ParentInstanceId
                        : done.Task.InstanceId;
                }

                var referrer = done.Task.AssignedBy.Length > 0 ? done.Task.AssignedBy : actor;
                next = await engine.AssignTo(referrer, input, cancellationToken);
            }
        }

        return new CompleteAndAssignToResult { Complete = done, Next = next };
    }
}
