namespace WorkflowEngine;

public sealed class MemoryStore : IStore
{
    private readonly object _mu = new();
    private readonly Dictionary<string, Definition> _defs = [];
    private readonly Dictionary<string, ProcessInstance> _instances = [];
    private readonly Dictionary<string, WorkflowTask> _tasks = [];

    public Task SaveDefinition(Definition def, CancellationToken cancellationToken = default)
    {
        lock (_mu)
            _defs[def.Id] = def.Clone();
        return Task.CompletedTask;
    }

    public Task<Definition> GetDefinition(string id, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            if (!_defs.TryGetValue(id, out var d))
                throw EngineException.NotFound();
            return Task.FromResult(d.Clone());
        }
    }

    public Task<Definition?> GetDefinitionByKey(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            tenantId = TenantContext.Normalize(tenantId);
            Definition? found = null;
            foreach (var d in _defs.Values)
            {
                if (TenantContext.Normalize(d.TenantId) == tenantId && d.Key == key)
                {
                    if (found is null || d.CreatedAt > found.CreatedAt)
                        found = d;
                }
            }
            return Task.FromResult(found?.Clone());
        }
    }

    public Task CreateInstance(ProcessInstance inst, CancellationToken cancellationToken = default)
    {
        lock (_mu)
            _instances[inst.Id] = inst.Clone();
        return Task.CompletedTask;
    }

    public Task<ProcessInstance> GetInstance(string id, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            if (!_instances.TryGetValue(id, out var inst))
                throw EngineException.NotFound();
            return Task.FromResult(inst.Clone());
        }
    }

    public Task UpdateInstance(ProcessInstance inst, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            if (!_instances.ContainsKey(inst.Id))
                throw EngineException.NotFound();
            _instances[inst.Id] = inst.Clone();
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProcessInstance>> ListRootInstances(string tenantId, string processKey, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            tenantId = TenantContext.Normalize(tenantId);
            var outList = _instances.Values
                .Where(inst => TenantContext.Normalize(inst.TenantId) == tenantId
                               && inst.DefinitionKey == processKey
                               && inst.ParentInstanceId == "")
                .Select(i => i.Clone())
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
            return Task.FromResult<IReadOnlyList<ProcessInstance>>(outList);
        }
    }

    public Task SaveTask(WorkflowTask task, CancellationToken cancellationToken = default)
    {
        lock (_mu)
            _tasks[task.Id] = task.Clone();
        return Task.CompletedTask;
    }

    public Task<WorkflowTask> GetTask(string id, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            if (!_tasks.TryGetValue(id, out var t))
                throw EngineException.NotFound();
            return Task.FromResult(t.Clone());
        }
    }

    public Task<WorkflowTask> TransitionTask(string id, IReadOnlyList<string> allowed, Action<WorkflowTask> apply, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            if (!_tasks.TryGetValue(id, out var t))
                throw EngineException.NotFound();
            if (!allowed.Contains(t.Status))
                throw EngineException.NotOpen();
            var cp = t.Clone();
            apply(cp);
            _tasks[id] = cp.Clone();
            return Task.FromResult(cp.Clone());
        }
    }

    public Task<IReadOnlyList<WorkflowTask>> ListTasks(TaskFilter filter, CancellationToken cancellationToken = default)
    {
        lock (_mu)
        {
            var outList = _tasks.Values
                .Where(t => Match(t, filter))
                .Select(t => t.Clone())
                .OrderBy(t => t.CreatedAt)
                .ToList();
            return Task.FromResult<IReadOnlyList<WorkflowTask>>(outList);
        }
    }

    private static bool Match(WorkflowTask t, TaskFilter f)
    {
        if (!string.IsNullOrEmpty(f.TenantId) && TenantContext.Normalize(t.TenantId) != TenantContext.Normalize(f.TenantId))
            return false;
        if (!string.IsNullOrEmpty(f.InstanceId) && t.InstanceId != f.InstanceId && t.ParentInstanceId != f.InstanceId)
            return false;
        if (!string.IsNullOrEmpty(f.Status) && t.Status != f.Status)
            return false;
        if (f.Statuses is { Count: > 0 } && !f.Statuses.Contains(t.Status))
            return false;
        if (!string.IsNullOrEmpty(f.ClaimedBy) && t.ClaimedBy != f.ClaimedBy)
            return false;
        if (!string.IsNullOrEmpty(f.GroupId))
            return t.AssigneeKind == AssigneeKind.Group && t.AssigneeId == f.GroupId;
        if (!string.IsNullOrEmpty(f.UserId))
        {
            if (t.AssigneeKind == AssigneeKind.User && t.AssigneeId == f.UserId)
                return true;
            if (t.AssigneeKind == AssigneeKind.Group && f.GroupIds is not null && f.GroupIds.Contains(t.AssigneeId))
                return true;
            return false;
        }
        return true;
    }
}
