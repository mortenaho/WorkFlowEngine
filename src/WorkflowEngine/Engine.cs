namespace WorkflowEngine;

public sealed class Engine
{
    private readonly IStore _store;
    private readonly IDirectory _dir;
    private readonly Func<DateTime> _clock;

    public Engine(IStore store, IDirectory directory, Func<DateTime>? clock = null)
    {
        _store = store;
        _dir = directory;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    private DateTime Now()
    {
        var t = _clock();
        return t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime();
    }

    public async Task<Definition> Register(string key, string name, CancellationToken cancellationToken = default)
    {
        key = key.Trim();
        if (key.Length == 0)
            throw EngineException.Invalid("process key is required");
        var existing = await _store.GetDefinitionByKey(TenantContext.Id, key, cancellationToken);
        if (existing is not null)
        {
            if (name.Length > 0 && existing.Name != name)
            {
                existing.Name = name;
                await _store.SaveDefinition(existing, cancellationToken);
            }
            return existing;
        }
        if (name.Length == 0)
            name = key;
        var def = new Definition
        {
            Id = Ids.New(),
            TenantId = TenantContext.Id,
            Key = key,
            Name = name,
            CreatedAt = Now(),
        };
        await _store.SaveDefinition(def, cancellationToken);
        return def;
    }

    public async Task<Definition> GetDefinitionByKey(string key, CancellationToken cancellationToken = default)
    {
        var def = await _store.GetDefinitionByKey(TenantContext.Id, key, cancellationToken);
        return def ?? throw EngineException.NotFound();
    }

    public Task<Definition> LatestDefinition(string key, CancellationToken cancellationToken = default)
        => GetDefinitionByKey(key, cancellationToken);

    public async Task<StartResult> Start(string processKey, string initiator, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        processKey = processKey.Trim();
        initiator = initiator.Trim();
        if (processKey.Length == 0)
            throw EngineException.Invalid("processKey is required");
        if (initiator.Length == 0)
            throw EngineException.Invalid("initiator is required");
        var def = await Register(processKey, "", cancellationToken);
        var now = Now();
        var inst = new ProcessInstance
        {
            Id = Ids.New(),
            TenantId = TenantContext.Id,
            DefinitionId = def.Id,
            DefinitionKey = def.Key,
            Status = InstanceStatus.Running,
            Parameters = VarsUtil.Clone(parameters),
            StartedBy = initiator,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _store.CreateInstance(inst, cancellationToken);
        return new StartResult { DefinitionKey = def.Key, InstanceId = inst.Id };
    }

    public async Task<ProcessInstance> GetInstance(string id, CancellationToken cancellationToken = default)
    {
        var inst = await _store.GetInstance(id, cancellationToken);
        if (TenantContext.Normalize(inst.TenantId) != TenantContext.Id)
            throw EngineException.ForbiddenTenant();
        return inst;
    }

    public async Task<ProcessList> ListByProcessKey(string processKey, CancellationToken cancellationToken = default)
    {
        processKey = processKey.Trim();
        if (processKey.Length == 0)
            throw EngineException.Invalid("processKey is required");
        var roots = await _store.ListRootInstances(TenantContext.Id, processKey, cancellationToken);
        var list = new ProcessList { ProcessKey = processKey, Instances = [] };
        foreach (var inst in roots)
        {
            if (TenantContext.Normalize(inst.TenantId) != TenantContext.Id)
                continue;
            var tasks = await _store.ListTasks(new TaskFilter
            {
                InstanceId = inst.Id,
                TenantId = TenantContext.Id,
            }, cancellationToken);
            list.Instances.Add(DetailFrom(inst, processKey, tasks));
        }
        list.Total = list.Instances.Count;
        return list;
    }

    private static ProcessInstanceDetail DetailFrom(ProcessInstance inst, string processKey, IReadOnlyList<WorkflowTask> tasks)
    {
        var d = new ProcessInstanceDetail
        {
            InstanceId = inst.Id,
            ProcessKey = processKey,
            DefinitionKey = inst.DefinitionKey,
            Initiator = inst.StartedBy,
            Status = inst.Status,
            Parameters = VarsUtil.Clone(inst.Parameters),
            CreatedAt = inst.CreatedAt,
            UpdatedAt = inst.UpdatedAt,
            Tasks = tasks.ToList(),
            TaskTotal = tasks.Count,
        };
        foreach (var t in tasks)
        {
            if (t.Status == TaskStatus.Done)
                d.TasksCompleted++;
            else
                d.TasksOpen++;
        }
        d.AllTasksCompleted = d.TaskTotal > 0 && d.TasksOpen == 0;
        return d;
    }

    public async Task<ReferResult> Refer(string actor, ReferInput input, CancellationToken cancellationToken = default)
    {
        actor = actor.Trim();
        if (actor.Length == 0)
            throw EngineException.Invalid("actor is required");
        var defKey = input.DefinitionKey.Trim();
        var parentId = input.ParentInstanceId.Trim();
        if (parentId.Length > 0)
        {
            var parent = await GetInstance(parentId, cancellationToken);
            if (defKey.Length == 0)
                defKey = parent.DefinitionKey;
            else if (defKey != parent.DefinitionKey)
                throw EngineException.Invalid("definitionKey does not match parent process");
        }
        if (defKey.Length == 0)
            throw EngineException.Invalid("definitionKey is required");
        var def = await _store.GetDefinitionByKey(TenantContext.Id, defKey, cancellationToken)
                  ?? throw EngineException.NotFound($"unknown definition {defKey} (start the process first)");

        var kind = input.ToKind;
        var ids = UniqueIds(input.ToIds);
        if (input.ToId.Length > 0)
            ids = UniqueIds(ids.Append(input.ToId));
        switch (kind)
        {
            case AssigneeKind.User:
                if (ids.Count != 1)
                    throw EngineException.Invalid("user referral needs exactly one id");
                break;
            case AssigneeKind.Group:
                if (ids.Count != 1)
                    throw EngineException.Invalid("group referral needs exactly one id");
                var members = await _dir.GroupMembers(ids[0], cancellationToken);
                if (members.Count == 0)
                    throw EngineException.EmptyGroup();
                break;
            case AssigneeKind.Users:
                if (ids.Count == 0)
                    throw EngineException.Invalid("users referral needs at least one id");
                kind = AssigneeKind.User;
                break;
            default:
                throw EngineException.Invalid("to.kind must be user, group, or users");
        }

        var now = Now();
        var inst = new ProcessInstance
        {
            Id = Ids.New(),
            TenantId = TenantContext.Id,
            DefinitionId = def.Id,
            DefinitionKey = def.Key,
            ParentInstanceId = parentId,
            Status = InstanceStatus.Running,
            Parameters = VarsUtil.Clone(input.Parameters),
            StartedBy = actor,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _store.CreateInstance(inst, cancellationToken);

        var tasks = new List<WorkflowTask>(ids.Count);
        foreach (var id in ids)
        {
            var t = new WorkflowTask
            {
                Id = Ids.New(),
                TenantId = TenantContext.Id,
                InstanceId = inst.Id,
                ParentInstanceId = parentId,
                DefinitionKey = def.Key,
                Title = input.Title,
                AssigneeKind = kind,
                AssigneeId = id,
                AssignedBy = actor,
                Status = TaskStatus.Open,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _store.SaveTask(t, cancellationToken);
            tasks.Add(t);
        }
        var result = new ReferResult
        {
            InstanceId = inst.Id,
            DefinitionKey = def.Key,
            Tasks = tasks,
        };
        if (tasks.Count == 1)
            result.Task = tasks[0];
        return result;
    }

    private static List<string> UniqueIds(IEnumerable<string>? ids)
    {
        var seen = new HashSet<string>();
        var outList = new List<string>();
        if (ids is null)
            return outList;
        foreach (var raw in ids)
        {
            var id = raw.Trim();
            if (id.Length == 0 || !seen.Add(id))
                continue;
            outList.Add(id);
        }
        return outList;
    }

    public async Task<IReadOnlyList<WorkflowTask>> PendingTasks(string userId, string groupId, CancellationToken cancellationToken = default)
    {
        userId = userId.Trim();
        groupId = groupId.Trim();
        if (userId.Length == 0 && groupId.Length == 0)
            throw EngineException.Invalid("user or group is required");
        if (groupId.Length > 0)
        {
            var tasks = await _store.ListTasks(new TaskFilter
            {
                TenantId = TenantContext.Id,
                GroupId = groupId,
                Statuses = [TaskStatus.Open, TaskStatus.Claimed],
            }, cancellationToken);
            return tasks;
        }
        var groups = await _dir.UserGroups(userId, cancellationToken);
        var open = await _store.ListTasks(new TaskFilter
        {
            UserId = userId,
            Status = TaskStatus.Open,
            TenantId = TenantContext.Id,
            GroupIds = groups,
        }, cancellationToken);
        var claimed = await _store.ListTasks(new TaskFilter
        {
            ClaimedBy = userId,
            Status = TaskStatus.Claimed,
            TenantId = TenantContext.Id,
        }, cancellationToken);
        return MergeTasks(open, claimed);
    }

    private static List<WorkflowTask> MergeTasks(params IReadOnlyList<WorkflowTask>[] parts)
    {
        var seen = new HashSet<string>();
        var outList = new List<WorkflowTask>();
        foreach (var list in parts)
        {
            foreach (var t in list)
            {
                if (!seen.Add(t.Id))
                    continue;
                outList.Add(t);
            }
        }
        return outList;
    }

    public async Task<WorkflowTask> GetTask(string id, CancellationToken cancellationToken = default)
    {
        var t = await _store.GetTask(id, cancellationToken);
        if (TenantContext.Normalize(t.TenantId) != TenantContext.Id)
            throw EngineException.ForbiddenTenant();
        return t;
    }

    public async Task<IReadOnlyList<WorkflowTask>> ListTasksByInstance(string instanceId, CancellationToken cancellationToken = default)
    {
        _ = await GetInstance(instanceId, cancellationToken);
        return await _store.ListTasks(new TaskFilter
        {
            InstanceId = instanceId,
            TenantId = TenantContext.Id,
        }, cancellationToken);
    }

    public async Task<Completion> Completion(string instanceId, CancellationToken cancellationToken = default)
    {
        _ = await GetInstance(instanceId, cancellationToken);
        var tasks = await _store.ListTasks(new TaskFilter
        {
            InstanceId = instanceId,
            TenantId = TenantContext.Id,
        }, cancellationToken);
        return CompletionOf(instanceId, tasks);
    }

    private static Completion CompletionOf(string instanceId, IReadOnlyList<WorkflowTask> tasks)
    {
        var owned = tasks.Where(t => t.InstanceId == instanceId).ToList();
        var c = new Completion { InstanceId = instanceId, Tasks = owned, Total = owned.Count };
        foreach (var t in owned)
        {
            if (t.Status == TaskStatus.Done)
                c.Completed++;
            else
                c.Open++;
        }
        c.AllCompleted = c.Total > 0 && c.Open == 0 && c.Completed == c.Total;
        return c;
    }

    public async Task<WorkflowTask> ClaimTask(string taskId, string actor, CancellationToken cancellationToken = default)
    {
        actor = actor.Trim();
        if (actor.Length == 0)
            throw EngineException.Invalid("actor is required");
        var task = await GetTask(taskId, cancellationToken);
        await CanAct(task, actor, cancellationToken);
        if (task.Status == TaskStatus.Claimed)
            throw EngineException.AlreadyClaimed();
        var now = Now();
        return await _store.TransitionTask(taskId, [TaskStatus.Open], t =>
        {
            t.Status = TaskStatus.Claimed;
            t.ClaimedBy = actor;
            t.UpdatedAt = now;
        }, cancellationToken);
    }

    public async Task<WorkflowTask> UnclaimTask(string taskId, string actor, CancellationToken cancellationToken = default)
    {
        actor = actor.Trim();
        if (actor.Length == 0)
            throw EngineException.Invalid("actor is required");
        var task = await GetTask(taskId, cancellationToken);
        if (task.Status != TaskStatus.Claimed)
            throw EngineException.NotClaimed();
        if (task.ClaimedBy != actor)
            throw EngineException.Forbidden();
        var now = Now();
        return await _store.TransitionTask(taskId, [TaskStatus.Claimed], t =>
        {
            t.Status = TaskStatus.Open;
            t.ClaimedBy = "";
            t.UpdatedAt = now;
        }, cancellationToken);
    }

    public async Task<CompleteResult> CompleteTask(string taskId, string actor, string note, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        actor = actor.Trim();
        if (actor.Length == 0)
            throw EngineException.Invalid("actor is required");
        var task = await GetTask(taskId, cancellationToken);
        await CanComplete(task, actor, cancellationToken);
        var now = Now();
        var updated = await _store.TransitionTask(taskId, [TaskStatus.Open, TaskStatus.Claimed], t =>
        {
            if (t.AssigneeKind == AssigneeKind.Group && t.ClaimedBy != actor)
                throw EngineException.NotClaimed();
            t.Status = TaskStatus.Done;
            t.Note = note;
            if (t.ClaimedBy.Length == 0)
                t.ClaimedBy = actor;
            t.UpdatedAt = now;
            t.CompletedAt = now;
        }, cancellationToken);
        var inst = await GetInstance(updated.InstanceId, cancellationToken);
        if (parameters is { Count: > 0 })
        {
            inst.Parameters = VarsUtil.Merge(inst.Parameters, parameters);
            inst.UpdatedAt = now;
            await _store.UpdateInstance(inst, cancellationToken);
        }
        var comp = await Completion(updated.InstanceId, cancellationToken);
        if (comp.AllCompleted && inst.Status != InstanceStatus.Completed)
        {
            inst.Status = InstanceStatus.Completed;
            inst.UpdatedAt = now;
            await _store.UpdateInstance(inst, cancellationToken);
        }
        return new CompleteResult { Task = updated, Completion = comp };
    }

    private async Task CanAct(WorkflowTask task, string actor, CancellationToken cancellationToken)
    {
        switch (task.AssigneeKind)
        {
            case AssigneeKind.User:
                if (task.AssigneeId != actor)
                    throw EngineException.Forbidden();
                break;
            case AssigneeKind.Group:
                if (!await _dir.IsMember(actor, task.AssigneeId, cancellationToken))
                    throw EngineException.Forbidden();
                break;
            default:
                throw EngineException.Forbidden();
        }
    }

    private async Task CanComplete(WorkflowTask task, string actor, CancellationToken cancellationToken)
    {
        await CanAct(task, actor, cancellationToken);
        if (task.AssigneeKind == AssigneeKind.Group)
        {
            if (task.Status != TaskStatus.Claimed || task.ClaimedBy != actor)
                throw EngineException.NotClaimed();
        }
        if (task.Status == TaskStatus.Claimed && task.ClaimedBy != actor)
            throw EngineException.Forbidden();
    }
}
