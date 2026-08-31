using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using TaskFlow.Client;
using TaskFlow.Domain;
using TaskFlow.Server;

namespace TaskFlow.Tests;

public class ClientSdkTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private TaskFlowClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWorkflow(Fixtures.NewEngine(), []);
        _app = builder.Build();
        _app.UseWorkflow();
        await _app.StartAsync();
        _client = new TaskFlowClient(_app.GetTestClient());
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task MicroservicesShareRunningEngineViaClient()
    {
        await _client.EnsureHealthy();

        var started = await _client.Start("purchase", "alice", new Dictionary<string, object?> { ["amount"] = 10 });
        Assert.Equal("purchase", started.DefinitionKey);

        var refer = await _client.AssignTo("alice", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            Title = "بررسی",
            ToKind = AssigneeKind.Users,
            ToIds = ["mortenaho", "cara"],
        });
        Assert.Equal(2, refer.Tasks.Count);

        var inbox = await _client.PendingTasks("mortenaho");
        Assert.Single(inbox);

        var orch = new TaskFlowOrchestrator(_client);
        AssignToResult? next = null;
        foreach (var task in refer.Tasks)
        {
            var advanced = await orch.CompleteAndAssignTo(
                task.Id,
                task.AssigneeId,
                "ok",
                _ => new AssignToInput
                {
                    Title = "حقوقی",
                    ToKind = AssigneeKind.Group,
                    ToId = "legal",
                });
            if (advanced.Next is not null)
                next = advanced.Next;
        }

        Assert.NotNull(next);
        Assert.NotNull(next.Task);
        Assert.Equal(AssigneeKind.Group, next.Task.AssigneeKind);

        var claimed = await _client.ClaimTask(next.Task!.Id, "mortenaho");
        Assert.Equal("mortenaho", claimed.ClaimedBy);

        var ended = await _client.CompleteAndEnd(claimed.Id, "mortenaho", "done");
        Assert.Equal(InstanceStatus.Completed, ended.Process.Status);

        var mine = await _client.ListUserProcesses("alice");
        Assert.True(mine.Total >= 1);
    }
}
