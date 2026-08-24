using WorkflowEngine;

namespace WorkflowEngine.Tests;

public class ScenarioTests
{
    [Fact]
    public async Task EmployeeTerminationScenario()
    {
        var eng = Fixtures.NewEngine();

        var emp1 = await eng.Start("employeeTermination", "alice", new Dictionary<string, object?>
        {
            ["employeeId"] = "1001",
            ["employeeName"] = "رضا محمدی",
        });
        var emp2 = await eng.Start("employeeTermination", "alice", new Dictionary<string, object?>
        {
            ["employeeId"] = "1002",
            ["employeeName"] = "سارا احمدی",
        });

        var list = await eng.ListByProcessKey("employeeTermination");
        Assert.Equal(2, list.Total);

        var legal = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = emp1.DefinitionKey,
            ParentInstanceId = emp1.InstanceId,
            Title = "بررسی حقوقی",
            ToKind = AssigneeKind.User,
            ToId = "bob",
        });
        var bobInbox = await eng.PendingTasks("bob", "");
        Assert.Single(bobInbox);

        var groupRef = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = emp2.DefinitionKey,
            ParentInstanceId = emp2.InstanceId,
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });
        await Assert.ThrowsAsync<EngineException>(() => eng.CompleteTask(groupRef.Task!.Id, "bob", ""));
        await eng.ClaimTask(groupRef.Task!.Id, "bob");
        var caraInbox = await eng.PendingTasks("cara", "");
        Assert.Empty(caraInbox);
        await eng.CompleteTask(groupRef.Task.Id, "bob", "ok");
        await eng.CompleteTask(legal.Task!.Id, "bob", "ok");

        var multi = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = emp1.DefinitionKey,
            ParentInstanceId = emp1.InstanceId,
            ToKind = AssigneeKind.Users,
            ToIds = ["bob", "cara", "dan"],
        });
        var before = await eng.Completion(multi.InstanceId);
        Assert.False(before.AllCompleted);
        Assert.Equal(3, before.Total);

        CompleteResult? last = null;
        foreach (var tk in multi.Tasks)
            last = await eng.CompleteTask(tk.Id, tk.AssigneeId, "تأیید شد");
        Assert.NotNull(last);
        Assert.True(last.Completion.AllCompleted);

        var final = await eng.ListByProcessKey("employeeTermination");
        Assert.Equal(2, final.Total);
    }
}
