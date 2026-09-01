using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Client;

/// <summary>
/// HTTP SDK for talking to a running <c>TaskFlow.Server</c>.
/// Several microservices can share one engine by pointing this client at the same base URL.
/// </summary>
public sealed class TaskFlowClient
{
    private readonly HttpClient _http;
    private readonly TaskFlowClientOptions _options;

    public TaskFlowClient(HttpClient http, IOptions<TaskFlowClientOptions> options)
        : this(http, options.Value)
    {
    }

    public TaskFlowClient(HttpClient http, TaskFlowClientOptions? options = null)
    {
        _http = http;
        _options = options ?? new TaskFlowClientOptions();
        if (_http.BaseAddress is null)
            _http.BaseAddress = _options.BaseAddress;
    }

    public Task<StartResult> Start(
        string processKey,
        string initiator,
        Dictionary<string, object?>? parameters = null,
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<StartResult>(
            HttpMethod.Post,
            "v1/processes/start",
            actor ?? initiator,
            new { processKey, initiator, parameters },
            tenantId,
            cancellationToken);

    public Task<ProcessList> ListByProcessKey(
        string processKey,
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<ProcessList>(
            HttpMethod.Get,
            $"v1/processes/{Uri.EscapeDataString(processKey)}/instances",
            actor ?? "",
            null,
            tenantId,
            cancellationToken);

    public Task<AssignToResult> AssignTo(
        string from,
        AssignToInput input,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<AssignToResult>(
            HttpMethod.Post,
            "v1/assignments",
            from,
            BuildAssignmentBody(from, input),
            tenantId,
            cancellationToken);

    public Task<IReadOnlyList<WorkflowTask>> PendingTasks(
        string user = "",
        string group = "",
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var qs = new List<string>();
        if (user.Length > 0) qs.Add($"user={Uri.EscapeDataString(user)}");
        if (group.Length > 0) qs.Add($"group={Uri.EscapeDataString(group)}");
        var path = qs.Count == 0 ? "v1/tasks" : "v1/tasks?" + string.Join('&', qs);
        return SendList<WorkflowTask>(HttpMethod.Get, path, actor ?? user, null, tenantId, cancellationToken);
    }

    public Task<WorkflowTask> GetTask(
        string taskId,
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<WorkflowTask>(HttpMethod.Get, $"v1/tasks/{Uri.EscapeDataString(taskId)}", actor ?? "", null, tenantId, cancellationToken);

    public Task<WorkflowTask> ClaimTask(
        string taskId,
        string actor,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<WorkflowTask>(
            HttpMethod.Post,
            $"v1/tasks/{Uri.EscapeDataString(taskId)}/claim",
            actor,
            new { from = actor },
            tenantId,
            cancellationToken);

    public Task<WorkflowTask> UnclaimTask(
        string taskId,
        string actor,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<WorkflowTask>(
            HttpMethod.Post,
            $"v1/tasks/{Uri.EscapeDataString(taskId)}/unclaim",
            actor,
            new { from = actor },
            tenantId,
            cancellationToken);

    public Task<CompleteResult> CompleteTask(
        string taskId,
        string actor,
        string note = "",
        Dictionary<string, object?>? parameters = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<CompleteResult>(
            HttpMethod.Post,
            $"v1/tasks/{Uri.EscapeDataString(taskId)}/complete",
            actor,
            new { from = actor, note, parameters },
            tenantId,
            cancellationToken);

    public Task<CompleteAndEndResult> CompleteAndEnd(
        string taskId,
        string actor,
        string note = "",
        Dictionary<string, object?>? parameters = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<CompleteAndEndResult>(
            HttpMethod.Post,
            $"v1/tasks/{Uri.EscapeDataString(taskId)}/complete-and-end",
            actor,
            new { from = actor, note, parameters },
            tenantId,
            cancellationToken);

    public Task<Completion> Completion(
        string instanceId,
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<Completion>(
            HttpMethod.Get,
            $"v1/instances/{Uri.EscapeDataString(instanceId)}/completion",
            actor ?? "",
            null,
            tenantId,
            cancellationToken);

    public Task<InstanceInfo> GetInstance(
        string instanceId,
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<InstanceInfo>(
            HttpMethod.Get,
            $"v1/instances/{Uri.EscapeDataString(instanceId)}",
            actor ?? "",
            null,
            tenantId,
            cancellationToken);

    public Task<IReadOnlyList<WorkflowTask>> ListTasksByInstance(
        string instanceId,
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => SendList<WorkflowTask>(
            HttpMethod.Get,
            $"v1/instances/{Uri.EscapeDataString(instanceId)}/tasks",
            actor ?? "",
            null,
            tenantId,
            cancellationToken);

    public Task<UserProcessList> ListUserProcesses(
        string user,
        string state = "",
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"v1/users/{Uri.EscapeDataString(user)}/processes";
        if (state.Length > 0)
            path += $"?state={Uri.EscapeDataString(state)}";
        return Send<UserProcessList>(HttpMethod.Get, path, actor ?? user, null, tenantId, cancellationToken);
    }

    public Task<Definition> RegisterDefinition(
        string key,
        string name = "",
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<Definition>(
            HttpMethod.Post,
            "v1/definitions",
            actor ?? "",
            new { key, name },
            tenantId,
            cancellationToken);

    public Task<Definition> GetDefinition(
        string key,
        string? actor = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
        => Send<Definition>(
            HttpMethod.Get,
            $"v1/definitions/{Uri.EscapeDataString(key)}",
            actor ?? "",
            null,
            tenantId,
            cancellationToken);

    public async Task EnsureHealthy(CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "health");
        using var res = await _http.SendAsync(req, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            throw new TaskFlowApiException(res.StatusCode, $"health check failed: {(int)res.StatusCode}", body);
        }
    }

    private async Task<T> Send<T>(
        HttpMethod method,
        string path,
        string actor,
        object? body,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        using var res = await SendRaw(method, path, actor, body, tenantId, cancellationToken);
        var value = await res.Content.ReadFromJsonAsync<T>(ClientJson.Options, cancellationToken);
        return value ?? throw new TaskFlowApiException(res.StatusCode, "empty response body");
    }

    private async Task<IReadOnlyList<T>> SendList<T>(
        HttpMethod method,
        string path,
        string actor,
        object? body,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        using var res = await SendRaw(method, path, actor, body, tenantId, cancellationToken);
        var value = await res.Content.ReadFromJsonAsync<List<T>>(ClientJson.Options, cancellationToken);
        return value ?? [];
    }

    private async Task<HttpResponseMessage> SendRaw(
        HttpMethod method,
        string path,
        string actor,
        object? body,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(method, path);
        if (actor.Length > 0)
            req.Headers.TryAddWithoutValidation("X-Actor-Id", actor);

        var tenant = tenantId ?? _options.TenantId;
        if (!string.IsNullOrEmpty(tenant) && !req.Headers.Contains("X-Tenant-Id"))
            req.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);

        if (!string.IsNullOrEmpty(_options.ApiKey) && !_http.DefaultRequestHeaders.Contains("X-API-Key"))
            req.Headers.TryAddWithoutValidation("X-API-Key", _options.ApiKey);

        if (body is not null)
            req.Content = JsonContent.Create(body, options: ClientJson.Options);

        var res = await _http.SendAsync(req, cancellationToken);
        if (res.IsSuccessStatusCode)
            return res;

        var text = await res.Content.ReadAsStringAsync(cancellationToken);
        var message = TryReadError(text) ?? $"TaskFlow API {(int)res.StatusCode} {res.ReasonPhrase}";
        res.Dispose();
        throw new TaskFlowApiException(res.StatusCode, message, text);
    }

    private static string? TryReadError(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();
        }
        catch (JsonException)
        {
            // ignore malformed bodies
        }

        return null;
    }

    private static object BuildAssignmentBody(string from, AssignToInput input) => new
    {
        definitionKey = input.DefinitionKey,
        parentInstanceId = input.ParentInstanceId,
        from,
        title = input.Title,
        parameters = input.Parameters,
        join = string.IsNullOrEmpty(input.Join) ? null : input.Join,
        onAllCompleted = input.OnAllCompleted is null ? null : BuildContinuationBody(input.OnAllCompleted),
        to = BuildAssigneeBody(input),
    };

    private static object BuildContinuationBody(AssignToInput input) => new
    {
        title = input.Title,
        parameters = input.Parameters,
        to = BuildAssigneeBody(input),
    };

    private static object BuildAssigneeBody(AssignToInput input) =>
        input.ToKind == AssigneeKind.Users || input.ToIds is { Count: > 0 }
            ? new { kind = string.IsNullOrEmpty(input.ToKind) ? AssigneeKind.Users : input.ToKind, ids = input.ToIds }
            : new { kind = input.ToKind, id = input.ToId };
}
