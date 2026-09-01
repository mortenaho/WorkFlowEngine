namespace TaskFlow.Application;

public sealed class ParallelJoinHandler(Engine engine) : ITaskCompletedHandler
{
    public async Task<AssignToResult?> HandleAsync(TaskCompletedEvent e, CancellationToken cancellationToken = default)
    {
        if (!e.Complete.Completion.AllCompleted)
            return null;

        var instanceId = e.Complete.Task.InstanceId;
        if (!await engine.TryMarkJoinAdvanced(instanceId, cancellationToken))
            return null;

        var inst = await engine.GetInstance(instanceId, cancellationToken);
        var join = InstanceJoinState.Read(inst);
        if (join is null)
            return null;

        var continuation = join.OnAllCompleted;
        var task = e.Complete.Task;
        if (continuation.DefinitionKey.Length == 0)
            continuation.DefinitionKey = task.DefinitionKey;
        if (continuation.ParentInstanceId.Length == 0)
        {
            continuation.ParentInstanceId = task.ParentInstanceId.Length > 0
                ? task.ParentInstanceId
                : task.InstanceId;
        }

        return await engine.AssignTo(join.Referrer, continuation, cancellationToken);
    }
}
