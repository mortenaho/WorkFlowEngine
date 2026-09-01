using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using TaskFlow.Server;

namespace TaskFlow.Tests;

public class HttpTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWorkflow(Fixtures.NewEngine(), []);
        _app = builder.Build();
        _app.UseWorkflow();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private async Task<HttpResponseMessage> DoJson(HttpMethod method, string path, string actor, object? body, string? tenant = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        if (!string.IsNullOrEmpty(tenant))
            req.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        if (body is not null)
            req.Content = JsonContent.Create(body, options: JsonConfig.Options);
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task HttpStartAssignInboxComplete()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "sara", new
        {
            processKey = "purchase",
            initiator = "sara",
            parameters = new Dictionary<string, object?> { ["amount"] = 10 },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var started = await w.Content.ReadFromJsonAsync<StartResult>(JsonConfig.Options);
        Assert.NotNull(started);
        Assert.Equal("purchase", started.DefinitionKey);
        Assert.False(string.IsNullOrEmpty(started.InstanceId));

        w = await DoJson(HttpMethod.Post, "/v1/assignments", "sara", new
        {
            definitionKey = started.DefinitionKey,
            parentInstanceId = started.InstanceId,
            title = "بررسی",
            to = new { kind = "users", ids = new[] { "mortenaho", "tina" } },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var refer = await w.Content.ReadFromJsonAsync<AssignToResult>(JsonConfig.Options);
        Assert.NotNull(refer);
        Assert.False(string.IsNullOrEmpty(refer.InstanceId));
        Assert.Equal(2, refer.Tasks.Count);

        w = await DoJson(HttpMethod.Get, "/v1/tasks?user=mortenaho", "mortenaho", null);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        var inbox = await w.Content.ReadFromJsonAsync<List<WorkflowTask>>(JsonConfig.Options);
        Assert.NotNull(inbox);
        Assert.Single(inbox);

        w = await DoJson(HttpMethod.Get, $"/v1/instances/{refer.InstanceId}/completion", "sara", null);
        var comp = await w.Content.ReadFromJsonAsync<Completion>(JsonConfig.Options);
        Assert.NotNull(comp);
        Assert.False(comp.AllCompleted);
        Assert.Equal(2, comp.Total);

        var mortenahoTask = refer.Tasks.First(t => t.AssigneeId == "mortenaho").Id;
        var tinaTask = refer.Tasks.First(t => t.AssigneeId != "mortenaho").Id;
        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{mortenahoTask}/complete", "mortenaho", new { note = "ok" });
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        var done = await w.Content.ReadFromJsonAsync<CompleteResult>(JsonConfig.Options);
        Assert.NotNull(done);
        Assert.False(done.Completion.AllCompleted);

        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{tinaTask}/complete", "tina", new { });
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        done = await w.Content.ReadFromJsonAsync<CompleteResult>(JsonConfig.Options);
        Assert.NotNull(done);
        Assert.True(done.Completion.AllCompleted);
    }

    [Fact]
    public async Task Health()
    {
        var w = await DoJson(HttpMethod.Get, "/health", "", null);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
    }

    [Fact]
    public async Task SwaggerAndOpenApi()
    {
        var swagger = await DoJson(HttpMethod.Get, "/swagger", "", null);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var html = await swagger.Content.ReadAsStringAsync();
        Assert.Contains("swagger-ui", html, StringComparison.OrdinalIgnoreCase);

        var spec = await DoJson(HttpMethod.Get, "/openapi.yaml", "", null);
        Assert.Equal(HttpStatusCode.OK, spec.StatusCode);
        var yaml = await spec.Content.ReadAsStringAsync();
        Assert.Contains("Workflow Engine", yaml);
    }

    [Fact]
    public async Task AssignToUsesFromInBody()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "", new
        {
            processKey = "purchase",
            initiator = "sara",
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var started = await w.Content.ReadFromJsonAsync<StartResult>(JsonConfig.Options);
        Assert.NotNull(started);

        w = await DoJson(HttpMethod.Post, "/v1/assignments", "", new
        {
            definitionKey = started.DefinitionKey,
            parentInstanceId = started.InstanceId,
            from = "sara",
            to = new { kind = "user", id = "mortenaho" },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var refer = await w.Content.ReadFromJsonAsync<AssignToResult>(JsonConfig.Options);
        Assert.NotNull(refer);
        Assert.NotNull(refer.Task);
        Assert.Equal("sara", refer.Task.AssignedBy);
    }

    [Fact]
    public async Task HttpListByProcessKey()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "hr", new
        {
            processKey = "employeeTermination",
            initiator = "hr",
            parameters = new Dictionary<string, object?> { ["employeeId"] = "1001" },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        w = await DoJson(HttpMethod.Post, "/v1/processes/start", "hr", new
        {
            processKey = "employeeTermination",
            initiator = "hr",
            parameters = new Dictionary<string, object?> { ["employeeId"] = "1002" },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);

        w = await DoJson(HttpMethod.Get, "/v1/processes/employeeTermination/instances", "hr", null);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        var list = await w.Content.ReadFromJsonAsync<ProcessList>(JsonConfig.Options);
        Assert.NotNull(list);
        Assert.Equal(2, list.Total);
        Assert.Equal(2, list.Instances.Count);
    }

    [Fact]
    public async Task HttpClaimUnclaimGroupTask()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "sara", new
        {
            processKey = "purchase",
            initiator = "sara",
        });
        var started = await w.Content.ReadFromJsonAsync<StartResult>(JsonConfig.Options);

        w = await DoJson(HttpMethod.Post, "/v1/assignments", "sara", new
        {
            definitionKey = started!.DefinitionKey,
            parentInstanceId = started.InstanceId,
            to = new { kind = "group", id = "legal" },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var refer = await w.Content.ReadFromJsonAsync<AssignToResult>(JsonConfig.Options);
        Assert.NotNull(refer?.Task);
        Assert.Equal(AssigneeKind.Group, refer.Task.AssigneeKind);

        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{refer.Task.Id}/complete", "mortenaho", new { note = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, w.StatusCode);

        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{refer.Task.Id}/claim", "mortenaho", new { from = "mortenaho" });
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        var claimed = await w.Content.ReadFromJsonAsync<WorkflowTask>(JsonConfig.Options);
        Assert.Equal(TaskStatus.Claimed, claimed!.Status);
        Assert.Equal("mortenaho", claimed.ClaimedBy);

        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{refer.Task.Id}/claim", "tina", new { from = "tina" });
        Assert.Equal(HttpStatusCode.Conflict, w.StatusCode);

        w = await DoJson(HttpMethod.Get, "/v1/tasks?user=tina", "tina", null);
        var tinaInbox = await w.Content.ReadFromJsonAsync<List<WorkflowTask>>(JsonConfig.Options);
        Assert.Empty(tinaInbox!);

        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{refer.Task.Id}/unclaim", "mortenaho", new { from = "mortenaho" });
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);

        w = await DoJson(HttpMethod.Get, "/v1/tasks?group=legal", "sara", null);
        var groupInbox = await w.Content.ReadFromJsonAsync<List<WorkflowTask>>(JsonConfig.Options);
        Assert.Single(groupInbox!);
        Assert.Equal(TaskStatus.Open, groupInbox[0].Status);
    }

    [Fact]
    public async Task HttpTenantIsolation()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "sara", new
        {
            processKey = "purchase",
            initiator = "sara",
        }, tenant: "acme");
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var started = await w.Content.ReadFromJsonAsync<StartResult>(JsonConfig.Options);

        w = await DoJson(HttpMethod.Get, $"/v1/instances/{started!.InstanceId}", "sara", null, tenant: "other");
        Assert.Equal(HttpStatusCode.Forbidden, w.StatusCode);

        w = await DoJson(HttpMethod.Get, "/v1/processes/purchase/instances", "sara", null, tenant: "other");
        var list = await w.Content.ReadFromJsonAsync<ProcessList>(JsonConfig.Options);
        Assert.Equal(0, list!.Total);

        w = await DoJson(HttpMethod.Get, $"/v1/instances/{started.InstanceId}", "sara", null, tenant: "acme");
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
    }

    [Fact]
    public async Task ApiKeyRequiredWhenConfigured()
    {
        await using var locked = await StartWithKeys(["secret"]);
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/tasks?user=mortenaho");
        req.Headers.TryAddWithoutValidation("X-Actor-Id", "mortenaho");
        var w = await locked.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, w.StatusCode);

        req = new HttpRequestMessage(HttpMethod.Get, "/v1/tasks?user=mortenaho");
        req.Headers.TryAddWithoutValidation("X-Actor-Id", "mortenaho");
        req.Headers.TryAddWithoutValidation("X-API-Key", "secret");
        w = await locked.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);

        req = new HttpRequestMessage(HttpMethod.Get, "/v1/tasks?user=mortenaho");
        req.Headers.TryAddWithoutValidation("X-Actor-Id", "mortenaho");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer secret");
        w = await locked.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);

        req = new HttpRequestMessage(HttpMethod.Get, "/health");
        w = await locked.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
    }

    [Fact]
    public async Task HttpCompleteAndEndAndUserProcesses()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "sara", new
        {
            processKey = "purchase",
            initiator = "sara",
        });
        var started = await w.Content.ReadFromJsonAsync<StartResult>(JsonConfig.Options);

        w = await DoJson(HttpMethod.Get, "/v1/users/sara/processes", "sara", null);
        var mine = await w.Content.ReadFromJsonAsync<UserProcessList>(JsonConfig.Options);
        Assert.Equal(1, mine!.NotStarted);
        Assert.Equal(0, mine.Open);
        Assert.Equal(1, mine.Total);

        w = await DoJson(HttpMethod.Post, "/v1/assignments", "sara", new
        {
            definitionKey = started!.DefinitionKey,
            parentInstanceId = started.InstanceId,
            to = new { kind = "users", ids = new[] { "mortenaho", "tina" } },
        });
        var refer = await w.Content.ReadFromJsonAsync<AssignToResult>(JsonConfig.Options);
        var mortenahoTask = refer!.Tasks.First(t => t.AssigneeId == "mortenaho").Id;
        var tinaTask = refer.Tasks.First(t => t.AssigneeId == "tina").Id;

        w = await DoJson(HttpMethod.Get, "/v1/users/sara/processes?state=open", "sara", null);
        mine = await w.Content.ReadFromJsonAsync<UserProcessList>(JsonConfig.Options);
        Assert.Equal(1, mine!.Open);
        Assert.Equal(1, mine.Total);
        Assert.Equal(started.InstanceId, mine.Instances[0].InstanceId);

        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{mortenahoTask}/complete-and-end", "mortenaho", new { note = "بسته شد" });
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        var ended = await w.Content.ReadFromJsonAsync<CompleteAndEndResult>(JsonConfig.Options);
        Assert.Equal(1, ended!.CancelledTasks);
        Assert.Equal(InstanceStatus.Completed, ended.Process.Status);

        w = await DoJson(HttpMethod.Get, $"/v1/tasks/{tinaTask}", "tina", null);
        var cancelled = await w.Content.ReadFromJsonAsync<WorkflowTask>(JsonConfig.Options);
        Assert.Equal(TaskStatus.Cancelled, cancelled!.Status);

        w = await DoJson(HttpMethod.Get, "/v1/tasks?user=tina", "tina", null);
        var tinaInbox = await w.Content.ReadFromJsonAsync<List<WorkflowTask>>(JsonConfig.Options);
        Assert.Empty(tinaInbox!);

        w = await DoJson(HttpMethod.Get, "/v1/users/sara/processes?state=closed", "sara", null);
        mine = await w.Content.ReadFromJsonAsync<UserProcessList>(JsonConfig.Options);
        Assert.Equal(1, mine!.Closed);
        Assert.Equal(1, mine.Total);
        Assert.Equal(0, mine.Open);
        Assert.Equal(0, mine.NotStarted);
    }

    private static async Task<Hosted> StartWithKeys(string[] keys)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddWorkflow(Fixtures.NewEngine(), keys);
        var app = builder.Build();
        app.UseWorkflow();
        await app.StartAsync();
        return new Hosted(app, app.GetTestClient());
    }

    private sealed class Hosted(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
