using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using TaskFlow.Client;
using TaskFlow.Server;

namespace TaskFlow.Tests;

public class OrchestratorClientTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private TaskFlowClient _client = null!;
    private TaskFlowOrchestrator _orchestrator = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWorkflow(Fixtures.NewEngine(), []);
        _app = builder.Build();
        _app.UseWorkflow();
        await _app.StartAsync();
        _client = new TaskFlowClient(_app.GetTestClient());
        _orchestrator = new TaskFlowOrchestrator(_client);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ClientOrchestratorAdvancesWhenAllCompleted()
    {
        var started = await _client.Start("purchase", "sara");
        var parallel = await _client.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Users,
            ToIds = ["mortenaho", "tina"],
        });

        var first = await _orchestrator.CompleteAndAssignTo(
            parallel.Tasks[0].Id,
            parallel.Tasks[0].AssigneeId,
            "",
            _ => new AssignToInput { ToKind = AssigneeKind.Group, ToId = "legal", Title = "legal" });
        Assert.False(first.Complete.Completion.AllCompleted);
        Assert.Null(first.Next);

        var second = await _orchestrator.CompleteAndAssignTo(
            parallel.Tasks[1].Id,
            parallel.Tasks[1].AssigneeId,
            "",
            _ => new AssignToInput { ToKind = AssigneeKind.Group, ToId = "legal", Title = "legal" });
        Assert.True(second.Complete.Completion.AllCompleted);
        Assert.NotNull(second.Next);
        Assert.Equal(AssigneeKind.Group, second.Next!.Task!.AssigneeKind);
        Assert.Equal("legal", second.Next.Task.AssigneeId);
    }

    [Fact]
    public async Task ClientExposesCoreOperations()
    {
        var started = await _client.Start("purchase", "sara");
        var inst = await _client.GetInstance(started.InstanceId);
        Assert.Equal("sara", inst.Initiator);

        var refer = await _client.AssignTo("sara", new AssignToInput
        {
            DefinitionKey = started.DefinitionKey,
            ParentInstanceId = started.InstanceId,
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });

        var task = await _client.GetTask(refer.Task!.Id);
        Assert.Equal("legal", task.AssigneeId);

        var tasks = await _client.ListTasksByInstance(refer.InstanceId);
        Assert.Single(tasks);

        var comp = await _client.Completion(refer.InstanceId);
        Assert.False(comp.AllCompleted);

        var list = await _client.ListByProcessKey("purchase");
        Assert.Equal(1, list.Total);

        await _client.ClaimTask(refer.Task.Id, "mortenaho");
        await _client.UnclaimTask(refer.Task.Id, "mortenaho");
    }
}
