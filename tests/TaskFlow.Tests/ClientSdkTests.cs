using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using TaskFlow.Application;
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

        var started = await _client.Start("purchase", "sara", new Dictionary<string, object?> { ["amount"] = 10 });
        Assert.Equal("purchase", started.DefinitionKey);

        var refer = await _client.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            Title = "بررسی",
            ToKind = AssigneeKind.Users,
            ToIds = ["mortenaho", "tina"],
            OnAllCompleted = new AssignToInput
            {
                Title = "حقوقی",
                ToKind = AssigneeKind.Group,
                ToId = "legal",
            },
        });
        Assert.Equal(2, refer.Tasks.Count);

        var inbox = await _client.PendingTasks("mortenaho");
        Assert.Single(inbox);

        var first = await _client.CompleteTaskWithOutcome(refer.Tasks[0].Id, refer.Tasks[0].AssigneeId, "ok");
        Assert.Equal(TaskCompletionStatus.WaitingForOthers, first.Status);
        Assert.Equal("waiting_for_others", first.StatusKey);

        var second = await _client.CompleteTaskWithOutcome(refer.Tasks[1].Id, refer.Tasks[1].AssigneeId, "ok");
        Assert.Equal(TaskCompletionStatus.AllDone, second.Status);
        Assert.NotNull(second.Next);
        Assert.Equal(AssigneeKind.Group, second.Next!.Task!.AssigneeKind);

        var claimed = await _client.ClaimTask(second.Next.Task!.Id, "mortenaho");
        Assert.Equal("mortenaho", claimed.ClaimedBy);

        var ended = await _client.CompleteAndEnd(claimed.Id, "mortenaho", "done");
        Assert.Equal(InstanceStatus.Completed, ended.Process.Status);

        var mine = await _client.ListUserProcesses("sara");
        Assert.True(mine.Total >= 1);
    }
}
