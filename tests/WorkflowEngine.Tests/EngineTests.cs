using WorkflowEngine;

namespace WorkflowEngine.Tests;

public class EngineTests
{
    [Fact]
    public async Task StartReturnsDefinitionKeyAndInstanceId()
    {
        var eng = Fixtures.NewEngine();
        var result = await eng.Start("purchase", "alice", new Dictionary<string, object?> { ["amount"] = 1e8 });
        Assert.Equal("purchase", result.DefinitionKey);
        Assert.False(string.IsNullOrEmpty(result.InstanceId));
        var inst = await eng.GetInstance(result.InstanceId);
        Assert.Equal("alice", inst.StartedBy);
    }

    [Fact]
    public async Task ReferToPerson()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "alice");
        var refer = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            Title = "بررسی",
            ToKind = AssigneeKind.User,
            ToId = "bob",
        });
        Assert.False(string.IsNullOrEmpty(refer.InstanceId));
        Assert.NotEqual(started.InstanceId, refer.InstanceId);
        Assert.NotNull(refer.Task);
        Assert.Equal("bob", refer.Task.AssigneeId);
        Assert.Equal(TaskStatus.Open, refer.Task.Status);
        Assert.Single(refer.Tasks);
    }

    [Fact]
    public async Task ReferToGroup()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "alice");
        var refer = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });
        Assert.Equal(AssigneeKind.Group, refer.Task!.AssigneeKind);
        Assert.Equal("legal", refer.Task.AssigneeId);

        var bob = await eng.PendingTasks("bob", "");
        Assert.Single(bob);
        var cara = await eng.PendingTasks("cara", "");
        Assert.Single(cara);
        var group = await eng.PendingTasks("", "legal");
        Assert.Single(group);

        await Assert.ThrowsAsync<EngineException>(() => eng.CompleteTask(refer.Task.Id, "bob", "ok"));
        var claimed = await eng.ClaimTask(refer.Task.Id, "bob");
        Assert.Equal(TaskStatus.Claimed, claimed.Status);
        Assert.Equal("bob", claimed.ClaimedBy);
        await Assert.ThrowsAsync<EngineException>(() => eng.ClaimTask(refer.Task.Id, "cara"));

        var caraInbox = await eng.PendingTasks("cara", "");
        Assert.Empty(caraInbox);
        var bobInbox = await eng.PendingTasks("bob", "");
        Assert.Single(bobInbox);
        await eng.CompleteTask(refer.Task.Id, "bob", "ok");
    }

    [Fact]
    public async Task UnclaimReturnsToGroup()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "alice");
        var refer = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });
        await eng.ClaimTask(refer.Task!.Id, "bob");
        await eng.UnclaimTask(refer.Task.Id, "bob");
        var claimed = await eng.ClaimTask(refer.Task.Id, "cara");
        Assert.Equal("cara", claimed.ClaimedBy);
    }

    [Fact]
    public async Task PendingTasksByUserAndGroup()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "alice");
        await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "bob",
        });
        await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Group,
            ToId = "finance",
        });
        var bob = await eng.PendingTasks("bob", "");
        Assert.Single(bob);
        var dan = await eng.PendingTasks("dan", "");
        Assert.Single(dan);
        var finance = await eng.PendingTasks("", "finance");
        Assert.Single(finance);
        Assert.Equal(AssigneeKind.Group, finance[0].AssigneeKind);
    }

    [Fact]
    public async Task MultiUserAllCompleted()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "alice");
        var refer = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Users,
            ToIds = ["bob", "cara", "dan"],
        });
        Assert.Equal(3, refer.Tasks.Count);
        var comp = await eng.Completion(refer.InstanceId);
        Assert.False(comp.AllCompleted);
        Assert.Equal(3, comp.Total);
        Assert.Equal(3, comp.Open);

        var byUser = refer.Tasks.ToDictionary(t => t.AssigneeId, t => t.Id);
        var afterBob = await eng.CompleteTask(byUser["bob"], "bob", "");
        Assert.False(afterBob.Completion.AllCompleted);
        Assert.Equal(1, afterBob.Completion.Completed);
        await eng.CompleteTask(byUser["cara"], "cara", "");
        var last = await eng.CompleteTask(byUser["dan"], "dan", "");
        Assert.True(last.Completion.AllCompleted);
        Assert.Equal(3, last.Completion.Completed);
        Assert.Equal(0, last.Completion.Open);
        var inst = await eng.GetInstance(refer.InstanceId);
        Assert.Equal(InstanceStatus.Completed, inst.Status);
    }

    [Fact]
    public async Task StartRequiresFields()
    {
        var eng = Fixtures.NewEngine();
        await Assert.ThrowsAsync<EngineException>(() => eng.Start("", "alice"));
        await Assert.ThrowsAsync<EngineException>(() => eng.Start("purchase", ""));
    }

    [Fact]
    public async Task CompleteForbiddenForOtherUser()
    {
        var eng = Fixtures.NewEngine();
        var started = await eng.Start("purchase", "alice");
        var refer = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ToKind = AssigneeKind.User,
            ToId = "bob",
        });
        await Assert.ThrowsAsync<EngineException>(() => eng.CompleteTask(refer.Task!.Id, "cara", ""));
    }

    [Fact]
    public async Task ListByProcessKeyReturnsEachStart()
    {
        var eng = Fixtures.NewEngine();
        var a = await eng.Start("employeeTermination", "hr", new Dictionary<string, object?> { ["employeeId"] = "1001" });
        var b = await eng.Start("employeeTermination", "hr", new Dictionary<string, object?> { ["employeeId"] = "1002" });
        await eng.Start("purchase", "alice");
        await eng.Refer("hr", new ReferInput
        {
            DefinitionKey = a.DefinitionKey,
            ParentInstanceId = a.InstanceId,
            ToKind = AssigneeKind.User,
            ToId = "bob",
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
}
