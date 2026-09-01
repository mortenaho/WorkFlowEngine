namespace TaskFlow.Tests;

public class EngineTests
{
    [Fact]
    public async Task StartReturnsDefinitionKeyAndInstanceId()
    {
        var eng = Fixtures.NewEngine();
        var result = await eng.Start("purchase", "sara", new Dictionary<string, object?> { ["amount"] = 1e8 });
        Assert.Equal("purchase", result.DefinitionKey);
        Assert.False(string.IsNullOrEmpty(result.InstanceId));
        var inst = await eng.GetInstance(result.InstanceId);
        Assert.Equal("sara", inst.StartedBy);
    }

    [Fact]
    public async Task AssignToPerson()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            Title = "بررسی",
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });
        Assert.False(string.IsNullOrEmpty(refer.InstanceId));
        Assert.NotEqual(started.InstanceId, refer.InstanceId);
        Assert.NotNull(refer.Task);
        Assert.Equal("mortenaho", refer.Task.AssigneeId);
        Assert.Equal(TaskStatus.Open, refer.Task.Status);
        Assert.Single(refer.Tasks);
    }

    [Fact]
    public async Task AssignToGroup()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });
        Assert.Equal(AssigneeKind.Group, refer.Task!.AssigneeKind);
        Assert.Equal("legal", refer.Task.AssigneeId);

        var mortenaho = await eng.PendingTasks("mortenaho", "");
        Assert.Single(mortenaho);
        var tina = await eng.PendingTasks("tina", "");
        Assert.Single(tina);
        var group = await eng.PendingTasks("", "legal");
        Assert.Single(group);

        await Assert.ThrowsAsync<EngineException>(() => eng.CompleteTask(refer.Task.Id, "mortenaho", "ok"));
        var claimed = await eng.ClaimTask(refer.Task.Id, "mortenaho");
        Assert.Equal(TaskStatus.Claimed, claimed.Status);
        Assert.Equal("mortenaho", claimed.ClaimedBy);
        await Assert.ThrowsAsync<EngineException>(() => eng.ClaimTask(refer.Task.Id, "tina"));

        var tinaInbox = await eng.PendingTasks("tina", "");
        Assert.Empty(tinaInbox);
        var mortenahoInbox = await eng.PendingTasks("mortenaho", "");
        Assert.Single(mortenahoInbox);
        await eng.CompleteTask(refer.Task.Id, "mortenaho", "ok");
    }

    [Fact]
    public async Task UnclaimReturnsToGroup()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });
        await eng.ClaimTask(refer.Task!.Id, "mortenaho");
        await eng.UnclaimTask(refer.Task.Id, "mortenaho");
        var claimed = await eng.ClaimTask(refer.Task.Id, "tina");
        Assert.Equal("tina", claimed.ClaimedBy);
    }

    [Fact]
    public async Task PendingTasksByUserAndGroup()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });
        await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Group,
            ToId = "finance",
        });
        var mortenaho = await eng.PendingTasks("mortenaho", "");
        Assert.Single(mortenaho);
        var hamid = await eng.PendingTasks("hamid", "");
        Assert.Single(hamid);
        var finance = await eng.PendingTasks("", "finance");
        Assert.Single(finance);
        Assert.Equal(AssigneeKind.Group, finance[0].AssigneeKind);
    }

    [Fact]
    public async Task MultiUserAllCompleted()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Users,
            ToIds = ["mortenaho", "tina", "hamid"],
        });
        Assert.Equal(3, refer.Tasks.Count);
        var comp = await eng.Completion(refer.InstanceId);
        Assert.False(comp.AllCompleted);
        Assert.Equal(3, comp.Total);
        Assert.Equal(3, comp.Open);

        var byUser = refer.Tasks.ToDictionary(t => t.AssigneeId, t => t.Id);
        var afterMortenaho = await eng.CompleteTask(byUser["mortenaho"], "mortenaho", "");
        Assert.False(afterMortenaho.Completion.AllCompleted);
        Assert.Equal(1, afterMortenaho.Completion.Completed);
        await eng.CompleteTask(byUser["tina"], "tina", "");
        var last = await eng.CompleteTask(byUser["hamid"], "hamid", "");
        Assert.True(last.Completion.AllCompleted);
        Assert.Equal(3, last.Completion.Completed);
        Assert.Equal(0, last.Completion.Open);
        var inst = await eng.GetInstance(refer.InstanceId);
        Assert.Equal(InstanceStatus.Completed, inst.Status);
    }

    [Fact]
    public async Task ParallelJoinAutoAdvancesOnCompleteTask()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var parallel = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Users,
            ToIds = ["mortenaho", "tina"],
            OnAllCompleted = new AssignToInput
            {
                Title = "legal",
                ToKind = AssigneeKind.Group,
                ToId = "legal",
            },
        });

        var first = await eng.CompleteTask(parallel.Tasks[0].Id, parallel.Tasks[0].AssigneeId, "");
        Assert.False(first.Completion.AllCompleted);
        Assert.Null(first.Next);

        var second = await eng.CompleteTask(parallel.Tasks[1].Id, parallel.Tasks[1].AssigneeId, "");
        Assert.True(second.Completion.AllCompleted);
        Assert.NotNull(second.Next);
        Assert.NotNull(second.Next!.Task);
        Assert.Equal(AssigneeKind.Group, second.Next.Task.AssigneeKind);
        Assert.Equal("legal", second.Next.Task.AssigneeId);
        Assert.Equal(started.InstanceId, second.Next.Task.ParentInstanceId);
        Assert.Equal("sara", second.Next.Task.AssignedBy);
    }

    [Fact]
    public async Task OnAllCompletedRequiresParallelUsers()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var ex = await Assert.ThrowsAsync<EngineException>(() => eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
            OnAllCompleted = new AssignToInput { ToKind = AssigneeKind.Group, ToId = "legal" },
        }));
        Assert.Equal(EngineErrorKind.Invalid, ex.Kind);
    }

    [Fact]
    public async Task OrchestratorAdvancesOnlyWhenAllCompleted()
    {
        var eng = Fixtures.NewEngine();
        var orch = new ProcessOrchestrator(eng);
        var started = await eng.Start("purchase", "sara");
        var parallel = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Users,
            ToIds = ["mortenaho", "tina"],
        });

        var first = await orch.CompleteAndAssignTo(
            parallel.Tasks[0].Id,
            parallel.Tasks[0].AssigneeId,
            "",
            _ => new AssignToInput { ToKind = AssigneeKind.Group, ToId = "legal", Title = "legal" });
        Assert.False(first.Complete.Completion.AllCompleted);
        Assert.Null(first.Next);

        var second = await orch.CompleteAndAssignTo(
            parallel.Tasks[1].Id,
            parallel.Tasks[1].AssigneeId,
            "",
            _ => new AssignToInput { ToKind = AssigneeKind.Group, ToId = "legal", Title = "legal" });
        Assert.True(second.Complete.Completion.AllCompleted);
        Assert.NotNull(second.Next);
        Assert.NotNull(second.Next.Task);
        Assert.Equal(AssigneeKind.Group, second.Next.Task.AssigneeKind);
        Assert.Equal("legal", second.Next.Task.AssigneeId);
        Assert.Equal(started.InstanceId, second.Next.Task.ParentInstanceId);
        Assert.Equal("sara", second.Next.Task.AssignedBy);
    }

    [Fact]
    public async Task StartRequiresFields()
    {
        var eng = Fixtures.NewEngine();
        await Assert.ThrowsAsync<EngineException>(() => eng.Start("", "sara"));
        await Assert.ThrowsAsync<EngineException>(() => eng.Start("purchase", ""));
    }

    [Fact]
    public async Task CompleteForbiddenForOtherUser()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });
        await Assert.ThrowsAsync<EngineException>(() => eng.CompleteTask(refer.Task!.Id, "tina", ""));
    }

    [Fact]
    public async Task ListByProcessKeyReturnsEachStart()
    {
        var eng = Fixtures.NewEngine();
        var a = await eng.Start("employeeTermination", "hr", new Dictionary<string, object?> { ["employeeId"] = "1001" });
        var b = await eng.Start("employeeTermination", "hr", new Dictionary<string, object?> { ["employeeId"] = "1002" });
        await eng.Start("purchase", "sara");
        await eng.AssignTo("hr", new AssignToInput
        {
            DefinitionKey = a.DefinitionKey,
            ParentInstanceId = a.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });

        var list = await eng.ListByProcessKey("employeeTermination");
        Assert.Equal("employeeTermination", list.ProcessKey);
        Assert.Equal(2, list.Total);

        var ids = list.Instances.ToDictionary(i => i.InstanceId, i => i.Parameters);
        Assert.All(list.Instances, inst =>
        {
            Assert.Equal("employeeTermination", inst.ProcessKey);
            Assert.Equal("hr", inst.Initiator);
        });
        Assert.Equal("1001", ids[a.InstanceId]!["employeeId"]);
        Assert.Equal("1002", ids[b.InstanceId]!["employeeId"]);

        var first = list.Instances.First(i => i.InstanceId == a.InstanceId);
        Assert.Equal(1, first.TaskTotal);
        Assert.Equal(1, first.TasksOpen);

        var empty = await eng.ListByProcessKey("unknown");
        Assert.Equal(0, empty.Total);
        Assert.NotNull(empty.Instances);
    }

    [Fact]
    public async Task AssignToUnknownDefinitionFails()
    {
        var eng = Fixtures.NewEngine();
        var ex = await Assert.ThrowsAsync<EngineException>(() => eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = "missing",
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        }));
        Assert.Equal(EngineErrorKind.NotFound, ex.Kind);
    }

    [Fact]
    public async Task AssignToEmptyGroupFails()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var ex = await Assert.ThrowsAsync<EngineException>(() => eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ToKind = AssigneeKind.Group,
            ToId = "unknown-group",
        }));
        Assert.Equal(EngineErrorKind.EmptyGroup, ex.Kind);
    }

    [Fact]
    public async Task OpenDirectoryAcceptsAnyUserAndGroup()
    {
        var eng = new Engine(new MemoryStore(), new OpenDirectory());
        var started = await eng.Start("purchase", "102");
        var referUser = await eng.AssignTo("102", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "205",
        });
        Assert.Equal("205", referUser.Task!.AssigneeId);

        var referGroup = await eng.AssignTo("102", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Group,
            ToId = "unit-17",
        });
        Assert.Equal("unit-17", referGroup.Task!.AssigneeId);

        await eng.ClaimTask(referGroup.Task.Id, "999");
        await eng.CompleteTask(referGroup.Task.Id, "999", "done");
    }

    [Fact]
    public async Task AssignToMismatchedDefinitionKeyFails()
    {
        var eng = Fixtures.NewEngine();
        var purchase = await eng.Start("purchase", "sara");
        await eng.Start("leave", "sara");
        var ex = await Assert.ThrowsAsync<EngineException>(() => eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = "leave",
            ParentInstanceId = purchase.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        }));
        Assert.Equal(EngineErrorKind.Invalid, ex.Kind);
    }

    [Fact]
    public async Task PersonalTaskCompletesWithoutClaim()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });
        var done = await eng.CompleteTask(refer.Task!.Id, "mortenaho", "ok");
        Assert.Equal(TaskStatus.Done, done.Task.Status);
        Assert.True(done.Completion.AllCompleted);
    }

    [Fact]
    public async Task ConcurrentClaimOnlyOneWins()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });

        var t1 = eng.ClaimTask(refer.Task!.Id, "mortenaho");
        var t2 = eng.ClaimTask(refer.Task.Id, "tina");
        var results = await Task.WhenAll(
            Wrap(t1),
            Wrap(t2));

        Assert.Single(results.Where(r => r.Ok));
        Assert.Single(results.Where(r => !r.Ok && r.Kind == EngineErrorKind.AlreadyClaimed));
        var claimed = results.Single(r => r.Ok).Task!;
        Assert.Equal(TaskStatus.Claimed, claimed.Status);
        Assert.True(claimed.ClaimedBy is "mortenaho" or "tina");
    }

    [Fact]
    public async Task TenantsAreIsolated()
    {
        var store = new MemoryStore();
        var dir = new StaticDirectory(
            ["sara", "mortenaho"],
            new Dictionary<string, IReadOnlyList<string>> { ["legal"] = ["mortenaho"] });
        var eng = new Engine(store, dir);

        string acmeId;
        using (TenantContext.Use("acme"))
        {
            var started = await eng.Start("purchase", "sara");
            acmeId = started.InstanceId;
            await eng.AssignTo("sara", new AssignToInput
            {
                DefinitionKey = started.DefinitionKey,
                ParentInstanceId = started.InstanceId,
                ToKind = AssigneeKind.User,
                ToId = "mortenaho",
            });
        }

        using (TenantContext.Use("other"))
        {
            var ex = await Assert.ThrowsAsync<EngineException>(() => eng.GetInstance(acmeId));
            Assert.Equal(EngineErrorKind.ForbiddenTenant, ex.Kind);
            var list = await eng.ListByProcessKey("purchase");
            Assert.Equal(0, list.Total);
            var started = await eng.Start("purchase", "sara");
            Assert.NotEqual(acmeId, started.InstanceId);
        }

        using (TenantContext.Use("acme"))
        {
            var inst = await eng.GetInstance(acmeId);
            Assert.Equal("sara", inst.StartedBy);
            var list = await eng.ListByProcessKey("purchase");
            Assert.Equal(1, list.Total);
        }
    }

    [Fact]
    public async Task CompleteAndEndClosesRootAndCancelsSiblings()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "sara");
        var mortenaho = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });
        var tina = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "tina",
        });

        var ended = await eng.CompleteAndEnd(mortenaho.Task!.Id, "mortenaho", "بسته شد");
        Assert.Equal(TaskStatus.Done, ended.Task.Status);
        Assert.Equal(1, ended.CancelledTasks);
        Assert.Equal(InstanceStatus.Completed, ended.Process.Status);
        Assert.Equal(started.InstanceId, ended.Process.InstanceId);
        Assert.Equal(0, ended.Process.TasksOpen);

        var tinaTask = await eng.GetTask(tina.Task!.Id);
        Assert.Equal(TaskStatus.Cancelled, tinaTask.Status);
        Assert.Empty(await eng.PendingTasks("tina", ""));

        var root = await eng.GetInstance(started.InstanceId);
        Assert.Equal(InstanceStatus.Completed, root.Status);

        var ex = await Assert.ThrowsAsync<EngineException>(() => eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "hamid",
        }));
        Assert.Equal(EngineErrorKind.Invalid, ex.Kind);
    }

    [Fact]
    public async Task ListUserProcessesByState()
    {
        var eng = Fixtures.NewEngine();
        await eng.Start("leave", "sara");
        var open = await eng.Start("purchase", "sara");
        await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = open.DefinitionKey,
            ParentInstanceId = open.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });
        var closing = await eng.Start("travel", "sara");
        var refer = await eng.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = closing.DefinitionKey,
            ParentInstanceId = closing.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "mortenaho",
        });
        await eng.CompleteAndEnd(refer.Task!.Id, "mortenaho", "ok");
        await eng.Start("purchase", "mortenaho");

        var all = await eng.ListUserProcesses("sara");
        Assert.Equal("sara", all.User);
        Assert.Equal(1, all.NotStarted);
        Assert.Equal(1, all.Open);
        Assert.Equal(1, all.Closed);
        Assert.Equal(3, all.Total);

        var notStarted = await eng.ListUserProcesses("sara", ProcessState.NotStarted);
        Assert.Equal(1, notStarted.Total);
        Assert.Equal("leave", notStarted.Instances[0].ProcessKey);
        Assert.Equal(0, notStarted.Instances[0].TaskTotal);

        var running = await eng.ListUserProcesses("sara", ProcessState.Open);
        Assert.Equal(1, running.Total);
        Assert.Equal(open.InstanceId, running.Instances[0].InstanceId);

        var closed = await eng.ListUserProcesses("sara", ProcessState.Closed);
        Assert.Equal(1, closed.Total);
        Assert.Equal(InstanceStatus.Completed, closed.Instances[0].Status);

        var mortenaho = await eng.ListUserProcesses("mortenaho");
        Assert.Equal(1, mortenaho.NotStarted);
        Assert.Equal(0, mortenaho.Open);
        Assert.Equal(0, mortenaho.Closed);
    }

    private static async Task<(bool Ok, EngineErrorKind? Kind, WorkflowTask? Task)> Wrap(Task<WorkflowTask> task)
    {
        try
        {
            return (true, null, await task);
        }
        catch (EngineException ex)
        {
            return (false, ex.Kind, null);
        }
    }
}
