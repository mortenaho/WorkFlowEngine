using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using WorkflowEngine;
using WorkflowEngine.Server;

namespace WorkflowEngine.Tests;

public class HttpTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        _app = builder.Build();
        _app.UseWorkflow(Fixtures.NewEngine(), []);
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private async Task<HttpResponseMessage> DoJson(HttpMethod method, string path, string actor, object? body)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.TryAddWithoutValidation("X-Actor-Id", actor);
        if (body is not null)
            req.Content = JsonContent.Create(body, options: JsonConfig.Options);
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task HttpStartReferInboxComplete()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "alice", new
        {
            processKey = "purchase",
            initiator = "alice",
            parameters = new Dictionary<string, object?> { ["amount"] = 10 },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var started = await w.Content.ReadFromJsonAsync<StartResult>(JsonConfig.Options);
        Assert.NotNull(started);
        Assert.Equal("purchase", started.DefinitionKey);
        Assert.False(string.IsNullOrEmpty(started.InstanceId));

        w = await DoJson(HttpMethod.Post, "/v1/referrals", "alice", new
        {
            definitionKey = started.DefinitionKey,
            parentInstanceId = started.InstanceId,
            title = "بررسی",
            to = new { kind = "users", ids = new[] { "bob", "cara" } },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var refer = await w.Content.ReadFromJsonAsync<ReferResult>(JsonConfig.Options);
        Assert.NotNull(refer);
        Assert.False(string.IsNullOrEmpty(refer.InstanceId));
        Assert.Equal(2, refer.Tasks.Count);

        w = await DoJson(HttpMethod.Get, "/v1/tasks?user=bob", "bob", null);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        var inbox = await w.Content.ReadFromJsonAsync<List<WorkflowTask>>(JsonConfig.Options);
        Assert.NotNull(inbox);
        Assert.Single(inbox);

        w = await DoJson(HttpMethod.Get, $"/v1/instances/{refer.InstanceId}/completion", "alice", null);
        var comp = await w.Content.ReadFromJsonAsync<Completion>(JsonConfig.Options);
        Assert.NotNull(comp);
        Assert.False(comp.AllCompleted);
        Assert.Equal(2, comp.Total);

        var bobTask = refer.Tasks.First(t => t.AssigneeId == "bob").Id;
        var caraTask = refer.Tasks.First(t => t.AssigneeId != "bob").Id;
        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{bobTask}/complete", "bob", new { note = "ok" });
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
        var done = await w.Content.ReadFromJsonAsync<CompleteResult>(JsonConfig.Options);
        Assert.NotNull(done);
        Assert.False(done.Completion.AllCompleted);

        w = await DoJson(HttpMethod.Post, $"/v1/tasks/{caraTask}/complete", "cara", new { });
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
    public async Task ReferUsesFromInBody()
    {
        var w = await DoJson(HttpMethod.Post, "/v1/processes/start", "", new
        {
            processKey = "purchase",
            initiator = "alice",
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var started = await w.Content.ReadFromJsonAsync<StartResult>(JsonConfig.Options);
        Assert.NotNull(started);

        w = await DoJson(HttpMethod.Post, "/v1/referrals", "", new
        {
            definitionKey = started.DefinitionKey,
            parentInstanceId = started.InstanceId,
            from = "alice",
            to = new { kind = "user", id = "bob" },
        });
        Assert.Equal(HttpStatusCode.Created, w.StatusCode);
        var refer = await w.Content.ReadFromJsonAsync<ReferResult>(JsonConfig.Options);
        Assert.NotNull(refer);
        Assert.NotNull(refer.Task);
        Assert.Equal("alice", refer.Task.AssignedBy);
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
    public async Task ApiKeyRequiredWhenConfigured()
    {
        await using var locked = await StartWithKeys(["secret"]);
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/tasks?user=bob");
        req.Headers.TryAddWithoutValidation("X-Actor-Id", "bob");
        var w = await locked.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, w.StatusCode);

        req = new HttpRequestMessage(HttpMethod.Get, "/v1/tasks?user=bob");
        req.Headers.TryAddWithoutValidation("X-Actor-Id", "bob");
        req.Headers.TryAddWithoutValidation("X-API-Key", "secret");
        w = await locked.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);

        req = new HttpRequestMessage(HttpMethod.Get, "/health");
        w = await locked.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, w.StatusCode);
    }

    private static async Task<Hosted> StartWithKeys(string[] keys)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseWorkflow(Fixtures.NewEngine(), keys);
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
