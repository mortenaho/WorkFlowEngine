using TaskFlow.Application;

namespace TaskFlow.Client;

/// <summary>
/// Same behaviour as <see cref="ProcessOrchestrator"/>, but over HTTP via <see cref="TaskFlowClient"/>.
/// </summary>
public sealed class TaskFlowOrchestrator(TaskFlowClient client)
{
    public async Task<AdvanceResult> CompleteAndAdvance(
        string taskId,
        string actor,
        string note,
        Func<CompleteResult, ReferInput?>? nextWhenAllCompleted = null,
        Dictionary<string, object?>? parameters = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var done = await client.CompleteTask(taskId, actor, note, parameters, tenantId, cancellationToken);
        ReferResult? next = null;
        if (done.Completion.AllCompleted && nextWhenAllCompleted is not null)
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
                next = await client.Refer(referrer, input, tenantId, cancellationToken);
            }
        }

        return new AdvanceResult { Complete = done, Next = next };
    }
}
