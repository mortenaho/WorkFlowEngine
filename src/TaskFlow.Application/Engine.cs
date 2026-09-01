namespace TaskFlow.Application;

public sealed class Engine
{
    private readonly IStore _store;
    private readonly IDirectory _dir;
    private readonly ITenantProvider _tenant;
    private readonly Func<DateTime> _clock;
    private readonly IReadOnlyList<ITaskCompletedHandler> _taskCompletedHandlers;

    public Engine(
        IStore store,
        IDirectory directory,
        Func<DateTime>? clock = null,
        ITenantProvider? tenant = null,
        IEnumerable<ITaskCompletedHandler>? taskCompletedHandlers = null)
    {
        _store = store;
        _dir = directory;
        _tenant = tenant ?? new AmbientTenantProvider();
        _clock = clock ?? (() => DateTime.UtcNow);
        var handlers = taskCompletedHandlers?.ToList() ?? [];
        if (handlers.Count == 0)
            handlers.Add(new ParallelJoinHandler(this));
        _taskCompletedHandlers = handlers;
    }

    private DateTime Now()
    {
        var t = _clock();
        return t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime();
    }

    private string TenantId => _tenant.Current;

    public async Task<Definition> Register(string key, string name, CancellationToken cancellationToken = default)
    {
        key = key.Trim();
        if (key.Length == 0)
            throw EngineException.Invalid("process key is required");
        var existing = await _store.GetDefinitionByKey(TenantId, key, cancellationToken);
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
            TenantId = TenantId,
            Key = key,
            Name = name,
            CreatedAt = Now(),
        };
        await _store.SaveDefinition(def, cancellationToken);
        return def;
    }

    public async Task<Definition> GetDefinitionByKey(string key, CancellationToken cancellationToken = default)
    {
        var def = await _store.GetDefinitionByKey(TenantId, key, cancellationToken);
        return def ?? throw EngineException.NotFound();
    }

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
            TenantId = TenantId,
            DefinitionId = def.Id,
            DefinitionKey = def.Key,
            Status = InstanceStatus.Running,
            Parameters = Vars.Clone(parameters),
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
        if (Tenant.Normalize(inst.TenantId) != TenantId)
            throw EngineException.ForbiddenTenant();
        return inst;
    }

    public async Task<ProcessList> ListByProcessKey(string processKey, CancellationToken cancellationToken = default)
    {
        processKey = processKey.Trim();
        if (processKey.Length == 0)
            throw EngineException.Invalid("processKey is required");
        var roots = await _store.ListRootInstances(TenantId, processKey, cancellationToken);
        var list = new ProcessList { ProcessKey = processKey, Instances = [] };
        foreach (var inst in roots)
        {
            if (Tenant.Normalize(inst.TenantId) != TenantId)
                continue;
            var tasks = await _store.ListTasks(new TaskFilter
            {
                InstanceId = inst.Id,
                TenantId = TenantId,
            }, cancellationToken);
            list.Instances.Add(DetailFrom(inst, processKey, tasks));
        }
        list.Total = list.Instances.Count;
        return list;
    }

    public async Task<UserProcessList> ListUserProcesses(string user, string state = "", CancellationToken cancellationToken = default)
    {
        user = user.Trim();
        if (user.Length == 0)
            throw EngineException.Invalid("user is required");
        state = state.Trim();
        if (state.Length > 0 && state is not (ProcessState.Open or ProcessState.Closed or ProcessState.NotStarted))
            throw EngineException.Invalid("state must be open, closed, or notStarted");

        var roots = await _store.ListRootInstancesByInitiator(TenantId, user, cancellationToken);
        var list = new UserProcessList { User = user, State = state };
        foreach (var inst in roots)
        {
            if (Tenant.Normalize(inst.TenantId) != TenantId)
                continue;
            var tasks = await _store.ListTasks(new TaskFilter
            {
                InstanceId = inst.Id,
                TenantId = TenantId,
            }, cancellationToken);
            var classified = ClassifyProcess(inst, tasks);
            if (classified == ProcessState.Open)
                list.Open++;
            else if (classified == ProcessState.Closed)
                list.Closed++;
            else
                list.NotStarted++;
            if (state.Length == 0 || classified == state)
                list.Instances.Add(DetailFrom(inst, inst.DefinitionKey, tasks));
        }
        list.Total = list.Instances.Count;
        return list;
    }

    private static string ClassifyProcess(ProcessInstance inst, IReadOnlyList<WorkflowTask> tasks)
    {
        if (inst.Status == InstanceStatus.Completed)
            return ProcessState.Closed;
        if (tasks.Count == 0)
            return ProcessState.NotStarted;
        return ProcessState.Open;
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
            Parameters = Vars.Clone(inst.Parameters),
            CreatedAt = inst.CreatedAt,
            UpdatedAt = inst.UpdatedAt,
            Tasks = tasks.ToList(),
            TaskTotal = tasks.Count,
        };
        foreach (var t in tasks)
        {
            if (t.Status == TaskStatus.Done)
                d.TasksCompleted++;
            else if (t.Status is TaskStatus.Open or TaskStatus.Claimed)
                d.TasksOpen++;
        }
        d.AllTasksCompleted = d.TaskTotal > 0 && d.TasksOpen == 0;
        return d;
    }

    public async Task<AssignToResult> AssignTo(string actor, AssignToInput input, CancellationToken cancellationToken = default)
    {
        actor = actor.Trim();
        if (actor.Length == 0)
            throw EngineException.Invalid("actor is required");
        var defKey = input.DefinitionKey.Trim();
        var parentId = input.ParentInstanceId.Trim();
        if (parentId.Length > 0)
        {
            var parent = await GetInstance(parentId, cancellationToken);
            if (parent.Status == InstanceStatus.Completed)
                throw EngineException.Invalid("process is already ended");
            if (defKey.Length == 0)
                defKey = parent.DefinitionKey;
            else if (defKey != parent.DefinitionKey)
                throw EngineException.Invalid("definitionKey does not match parent process");
        }
        if (defKey.Length == 0)
            throw EngineException.Invalid("definitionKey is required");
        var def = await _store.GetDefinitionByKey(TenantId, defKey, cancellationToken)
                  ?? throw EngineException.NotFound($"unknown definition {defKey} (start the process first)");

        var kind = input.ToKind;
        var ids = UniqueIds(input.ToIds);
        if (input.ToId.Length > 0)
            ids = UniqueIds(ids.Append(input.ToId));
        switch (kind)
        {
            case AssigneeKind.User:
                if (ids.Count != 1)
                    throw EngineException.Invalid("user assignment needs exactly one id");
                break;
            case AssigneeKind.Group:
                if (ids.Count != 1)
                    throw EngineException.Invalid("group assignment needs exactly one id");
                if (_dir.EnforcesMembership)
                {
                    var members = await _dir.GroupMembers(ids[0], cancellationToken);
                    if (members.Count == 0)
                        throw EngineException.EmptyGroup();
                }
                break;
            case AssigneeKind.Users:
                if (ids.Count == 0)
                    throw EngineException.Invalid("users assignment needs at least one id");
                kind = AssigneeKind.User;
                break;
            default:
                throw EngineException.Invalid("to.kind must be user, group, or users");
        }

        var now = Now();
        var inst = new ProcessInstance
        {
            Id = Ids.New(),
            TenantId = TenantId,
            DefinitionId = def.Id,
            DefinitionKey = def.Key,
            ParentInstanceId = parentId,
            Status = InstanceStatus.Running,
            Parameters = Vars.Clone(input.Parameters),
            StartedBy = actor,
            CreatedAt = now,
            UpdatedAt = now,
        };
        AttachJoinIfNeeded(inst, input, ids.Count, actor);
        await _store.CreateInstance(inst, cancellationToken);

        var tasks = new List<WorkflowTask>(ids.Count);
        foreach (var id in ids)
        {
            var t = new WorkflowTask
            {
                Id = Ids.New(),
                TenantId = TenantId,
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
        var result = new AssignToResult
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
            return await _store.ListTasks(new TaskFilter
            {
                TenantId = TenantId,
                GroupId = groupId,
                Statuses = [TaskStatus.Open, TaskStatus.Claimed],
            }, cancellationToken);
        }
        var groups = await _dir.UserGroups(userId, cancellationToken);
        var open = await _store.ListTasks(new TaskFilter
        {
            UserId = userId,
            Status = TaskStatus.Open,
            TenantId = TenantId,
            GroupIds = groups,
        }, cancellationToken);
        var claimed = await _store.ListTasks(new TaskFilter
        {
            ClaimedBy = userId,
            Status = TaskStatus.Claimed,
            TenantId = TenantId,
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
        if (Tenant.Normalize(t.TenantId) != TenantId)
            throw EngineException.ForbiddenTenant();
        return t;
    }

    public async Task<IReadOnlyList<WorkflowTask>> ListTasksByInstance(string instanceId, CancellationToken cancellationToken = default)
    {
        _ = await GetInstance(instanceId, cancellationToken);
        return await _store.ListTasks(new TaskFilter
        {
            InstanceId = instanceId,
            TenantId = TenantId,
        }, cancellationToken);
    }

    public async Task<Completion> Completion(string instanceId, CancellationToken cancellationToken = default)
    {
        _ = await GetInstance(instanceId, cancellationToken);
        var tasks = await _store.ListTasks(new TaskFilter
        {
            InstanceId = instanceId,
            TenantId = TenantId,
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
            else if (t.Status is TaskStatus.Open or TaskStatus.Claimed)
                c.Open++;
        }
        c.AllCompleted = c.Total > 0 && c.Open == 0;
        return c;
    }

    public async Task<WorkflowTask> ClaimTask(string taskId, string actor, CancellationToken cancellationToken = default)
    {
        actor = actor.Trim();
        if (actor.Length == 0)
            throw EngineException.Invalid("actor is required");
        var task = await GetTask(taskId, cancellationToken);
        await CanAct(task, actor, cancellationToken);
        var now = Now();
        try
        {
            return await _store.TransitionTask(taskId, [TaskStatus.Open], t =>
            {
                t.Status = TaskStatus.Claimed;
                t.ClaimedBy = actor;
                t.UpdatedAt = now;
            }, cancellationToken);
        }
        catch (EngineException ex) when (ex.Kind == EngineErrorKind.NotOpen)
        {
            var current = await GetTask(taskId, cancellationToken);
            if (current.Status == TaskStatus.Claimed)
                throw EngineException.AlreadyClaimed();
            throw;
        }
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
            inst.Parameters = Vars.Merge(inst.Parameters, parameters);
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

        var result = new CompleteResult { Task = updated, Completion = comp };
        foreach (var handler in _taskCompletedHandlers)
        {
            var next = await handler.HandleAsync(new TaskCompletedEvent { Complete = result }, cancellationToken);
            if (next is not null && result.Next is null)
                result.Next = next;
        }

        return result;
    }

    public async Task<TaskCompletionOutcome> CompleteTaskWithOutcome(
        string taskId,
        string actor,
        string note,
        Dictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var result = await CompleteTask(taskId, actor, note, parameters, cancellationToken);
        return TaskCompletionOutcome.From(result);
    }

    internal async Task<bool> TryMarkJoinAdvanced(string instanceId, CancellationToken cancellationToken = default)
    {
        var inst = await GetInstance(instanceId, cancellationToken);
        var join = InstanceJoinState.Read(inst);
        if (join is null || join.Advanced)
            return false;

        InstanceJoinState.MarkAdvanced(inst);
        inst.UpdatedAt = Now();
        await _store.UpdateInstance(inst, cancellationToken);
        return true;
    }

    private static void AttachJoinIfNeeded(ProcessInstance inst, AssignToInput input, int assigneeCount, string actor)
    {
        if (input.OnAllCompleted is null)
            return;

        if (input.ToKind != AssigneeKind.Users || assigneeCount < 2)
            throw EngineException.Invalid("onAllCompleted requires to.kind=users with at least two ids");

        var mode = input.Join.Trim();
        if (mode.Length == 0)
            mode = JoinMode.All;
        if (mode != JoinMode.All)
            throw EngineException.Invalid($"unsupported join mode: {mode}");

        InstanceJoinState.Attach(inst, actor, mode, input.OnAllCompleted);
    }

    public async Task<CompleteAndEndResult> CompleteAndEnd(string taskId, string actor, string note, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var done = await CompleteTask(taskId, actor, note, parameters, cancellationToken);
        var inst = await GetInstance(done.Task.InstanceId, cancellationToken);
        var root = await RootOf(inst, cancellationToken);
        var now = Now();
        var cancelled = await CancelOpenTasks(root.Id, done.Task.Id, now, cancellationToken);
        await EndInstanceTree(root, now, cancellationToken);
        root = await GetInstance(root.Id, cancellationToken);
        var tasks = await _store.ListTasks(new TaskFilter
        {
            InstanceId = root.Id,
            TenantId = TenantId,
        }, cancellationToken);
        return new CompleteAndEndResult
        {
            Task = done.Task,
            Completion = done.Completion,
            Process = DetailFrom(root, root.DefinitionKey, tasks),
            CancelledTasks = cancelled,
        };
    }

    private async Task<ProcessInstance> RootOf(ProcessInstance inst, CancellationToken cancellationToken)
    {
        if (inst.ParentInstanceId.Length == 0)
            return inst;
        return await GetInstance(inst.ParentInstanceId, cancellationToken);
    }

    private async Task<int> CancelOpenTasks(string rootId, string exceptTaskId, DateTime now, CancellationToken cancellationToken)
    {
        var tasks = await _store.ListTasks(new TaskFilter
        {
            InstanceId = rootId,
            TenantId = TenantId,
            Statuses = [TaskStatus.Open, TaskStatus.Claimed],
        }, cancellationToken);
        var n = 0;
        foreach (var t in tasks)
        {
            if (t.Id == exceptTaskId)
                continue;
            try
            {
                await _store.TransitionTask(t.Id, [TaskStatus.Open, TaskStatus.Claimed], x =>
                {
                    x.Status = TaskStatus.Cancelled;
                    x.UpdatedAt = now;
                }, cancellationToken);
                n++;
            }
            catch (EngineException ex) when (ex.Kind == EngineErrorKind.NotOpen)
            {
            }
        }
        return n;
    }

    private async Task EndInstanceTree(ProcessInstance root, DateTime now, CancellationToken cancellationToken)
    {
        async Task EndOne(ProcessInstance inst)
        {
            if (inst.Status == InstanceStatus.Completed)
                return;
            inst.Status = InstanceStatus.Completed;
            inst.UpdatedAt = now;
            await _store.UpdateInstance(inst, cancellationToken);
        }
        await EndOne(root);
        foreach (var child in await _store.ListChildInstances(root.Id, cancellationToken))
            await EndOne(child);
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
