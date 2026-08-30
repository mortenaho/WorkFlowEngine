namespace TaskFlow.Application;

public interface IStore
{
    Task SaveDefinition(Definition def, CancellationToken cancellationToken = default);
    Task<Definition?> GetDefinitionByKey(string tenantId, string key, CancellationToken cancellationToken = default);

    Task CreateInstance(ProcessInstance inst, CancellationToken cancellationToken = default);
    Task<ProcessInstance> GetInstance(string id, CancellationToken cancellationToken = default);
    Task UpdateInstance(ProcessInstance inst, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessInstance>> ListRootInstances(string tenantId, string processKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessInstance>> ListRootInstancesByInitiator(string tenantId, string initiator, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessInstance>> ListChildInstances(string parentId, CancellationToken cancellationToken = default);

    Task SaveTask(WorkflowTask task, CancellationToken cancellationToken = default);
    Task<WorkflowTask> GetTask(string id, CancellationToken cancellationToken = default);
    Task<WorkflowTask> TransitionTask(string id, IReadOnlyList<string> allowed, Action<WorkflowTask> apply, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTask>> ListTasks(TaskFilter filter, CancellationToken cancellationToken = default);
}
