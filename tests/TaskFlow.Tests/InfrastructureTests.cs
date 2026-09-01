namespace TaskFlow.Tests;

public class InfrastructureTests
{
    [Fact]
    public async Task MemoryStorePersistsAndClonesDefinitions()
    {
        var store = new MemoryStore();
        var def = new Definition
        {
            Id = "d1",
            TenantId = "acme",
            Key = "purchase",
            Name = "Purchase",
            CreatedAt = DateTime.UtcNow,
        };
        await store.SaveDefinition(def);

        var loaded = await store.GetDefinitionByKey("acme", "purchase");
        Assert.NotNull(loaded);
        Assert.Equal("d1", loaded.Id);
        loaded!.Name = "Changed";
        var again = await store.GetDefinitionByKey("acme", "purchase");
        Assert.Equal("Purchase", again!.Name);
    }

    [Fact]
    public async Task MemoryStoreReturnsLatestDefinitionForKey()
    {
        var store = new MemoryStore();
        var older = new Definition { Id = "d1", TenantId = "acme", Key = "k", CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        var newer = new Definition { Id = "d2", TenantId = "acme", Key = "k", CreatedAt = DateTime.UtcNow };
        await store.SaveDefinition(older);
        await store.SaveDefinition(newer);

        var loaded = await store.GetDefinitionByKey("acme", "k");
        Assert.Equal("d2", loaded!.Id);
    }

    [Fact]
    public async Task MemoryStoreIsolatesTenants()
    {
        var store = new MemoryStore();
        await store.SaveDefinition(new Definition { Id = "d1", TenantId = "a", Key = "k" });
        Assert.Null(await store.GetDefinitionByKey("b", "k"));
        Assert.NotNull(await store.GetDefinitionByKey("a", "k"));
    }

    [Fact]
    public async Task MemoryStoreInstanceAndTaskLifecycle()
    {
        var store = new MemoryStore();
        var inst = new ProcessInstance
        {
            Id = "i1",
            TenantId = "default",
            DefinitionKey = "purchase",
            StartedBy = "sara",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await store.CreateInstance(inst);

        var loaded = await store.GetInstance("i1");
        Assert.Equal("sara", loaded.StartedBy);

        loaded.Status = InstanceStatus.Completed;
        await store.UpdateInstance(loaded);
        var updated = await store.GetInstance("i1");
        Assert.Equal(InstanceStatus.Completed, updated.Status);

        var task = new WorkflowTask
        {
            Id = "t1",
            TenantId = "default",
            InstanceId = "i1",
            AssigneeKind = AssigneeKind.User,
            AssigneeId = "mortenaho",
            Status = TaskStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await store.SaveTask(task);
        var got = await store.GetTask("t1");
        Assert.Equal("mortenaho", got.AssigneeId);

        var transitioned = await store.TransitionTask("t1", [TaskStatus.Open], t =>
        {
            t.Status = TaskStatus.Claimed;
            t.ClaimedBy = "mortenaho";
        });
        Assert.Equal(TaskStatus.Claimed, transitioned.Status);

        await Assert.ThrowsAsync<EngineException>(() =>
            store.TransitionTask("t1", [TaskStatus.Open], _ => { }));
    }

    [Fact]
    public async Task MemoryStoreListFilters()
    {
        var store = new MemoryStore();
        var root = new ProcessInstance
        {
            Id = "root",
            TenantId = "default",
            DefinitionKey = "purchase",
            StartedBy = "sara",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var child = new ProcessInstance
        {
            Id = "child",
            TenantId = "default",
            DefinitionKey = "purchase",
            ParentInstanceId = "root",
            StartedBy = "sara",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await store.CreateInstance(root);
        await store.CreateInstance(child);

        var roots = await store.ListRootInstances("default", "purchase");
        Assert.Single(roots);
        Assert.Equal("root", roots[0].Id);

        var byInitiator = await store.ListRootInstancesByInitiator("default", "sara");
        Assert.Single(byInitiator);

        var children = await store.ListChildInstances("root");
        Assert.Single(children);
        Assert.Equal("child", children[0].Id);

        await store.SaveTask(new WorkflowTask
        {
            Id = "g1",
            TenantId = "default",
            InstanceId = "child",
            AssigneeKind = AssigneeKind.Group,
            AssigneeId = "legal",
            Status = TaskStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await store.SaveTask(new WorkflowTask
        {
            Id = "u1",
            TenantId = "default",
            InstanceId = "child",
            AssigneeKind = AssigneeKind.User,
            AssigneeId = "mortenaho",
            Status = TaskStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        var userInbox = await store.ListTasks(new TaskFilter { UserId = "mortenaho", GroupIds = ["legal"] });
        Assert.Equal(2, userInbox.Count);

        var groupInbox = await store.ListTasks(new TaskFilter { GroupId = "legal" });
        Assert.Single(groupInbox);

        await Assert.ThrowsAsync<EngineException>(() => store.GetInstance("missing"));
        await Assert.ThrowsAsync<EngineException>(() => store.GetTask("missing"));
    }

    [Fact]
    public async Task StaticDirectoryMembership()
    {
        var dir = new StaticDirectory(
            ["sara", "mortenaho"],
            new Dictionary<string, IReadOnlyList<string>> { ["legal"] = ["mortenaho"] });

        Assert.True(dir.EnforcesMembership);
        Assert.True(await dir.UserExists("sara"));
        Assert.False(await dir.UserExists("unknown"));

        var members = await dir.GroupMembers("legal");
        Assert.Single(members);
        Assert.Equal("mortenaho", members[0]);

        Assert.Empty(await dir.GroupMembers("empty"));

        var groups = await dir.UserGroups("mortenaho");
        Assert.Contains("legal", groups);

        Assert.True(await dir.IsMember("mortenaho", "legal"));
        Assert.False(await dir.IsMember("sara", "legal"));
    }

    [Fact]
    public async Task OpenDirectoryAcceptsOpaqueIds()
    {
        var dir = new OpenDirectory();
        Assert.False(dir.EnforcesMembership);
        Assert.True(await dir.UserExists("102"));
        Assert.False(await dir.UserExists(""));

        Assert.Empty(await dir.GroupMembers("unit-17"));
        Assert.Empty(await dir.UserGroups("102"));
        Assert.True(await dir.IsMember("999", "unit-17"));
        Assert.False(await dir.IsMember("", "unit-17"));
    }
}
