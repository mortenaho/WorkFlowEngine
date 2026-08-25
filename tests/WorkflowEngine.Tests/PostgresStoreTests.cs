namespace WorkflowEngine.Tests;

public class PostgresStoreTests
{
    [Fact]
    public async Task PostgresStartReferComplete()
    {
        var dsn = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrEmpty(dsn))
            return;

        await using var store = await PostgresStore.Open(dsn);
        var dir = new StaticDirectory(["alice", "bob"], new Dictionary<string, IReadOnlyList<string>>
        {
            ["legal"] = ["bob"],
        });
        var eng = new Engine(store, dir);
        using var tenant = TenantContext.Use("test-" + Ids.New()[..8]);
        var started = await eng.Start("purchase-pg", "alice", new Dictionary<string, object?> { ["n"] = 1 });
        var refer = await eng.Refer("alice", new ReferInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Users,
            ToIds = ["alice", "bob"],
        });
        await eng.CompleteTask(refer.Tasks[0].Id, refer.Tasks[0].AssigneeId, "");
        var last = await eng.CompleteTask(refer.Tasks[1].Id, refer.Tasks[1].AssigneeId, "");
        Assert.True(last.Completion.AllCompleted);
    }
}
