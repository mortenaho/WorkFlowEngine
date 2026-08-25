using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using WorkflowEngine;

namespace WorkflowEngine.Server;

public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = Create();

    public static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { OmitEmptyValues },
            },
        };
        return o;
    }

    private static void OmitEmptyValues(JsonTypeInfo info)
    {
        foreach (var prop in info.Properties)
        {
            if (prop.PropertyType == typeof(string))
            {
                prop.ShouldSerialize = (_, val) => !string.IsNullOrEmpty(val as string);
            }
            else if (prop.PropertyType == typeof(Dictionary<string, object?>))
            {
                prop.ShouldSerialize = (_, val) => val is Dictionary<string, object?> { Count: > 0 };
            }
        }
    }
}

public static class ErrorMapping
{
    public static int StatusCode(EngineException ex) => ex.Kind switch
    {
        EngineErrorKind.NotFound => StatusCodes.Status404NotFound,
        EngineErrorKind.Forbidden or EngineErrorKind.ForbiddenTenant => StatusCodes.Status403Forbidden,
        EngineErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        EngineErrorKind.Invalid or EngineErrorKind.NotOpen or EngineErrorKind.EmptyGroup or EngineErrorKind.NotClaimed
            => StatusCodes.Status400BadRequest,
        EngineErrorKind.Conflict or EngineErrorKind.AlreadyClaimed => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}

public sealed class RegisterDefinitionRequest
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class StartProcessRequest
{
    public string ProcessKey { get; set; } = "";
    public string Initiator { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
}

public sealed class AssigneeRequest
{
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public List<string>? Ids { get; set; }
}

public sealed class CreateReferralRequest
{
    public string DefinitionKey { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string From { get; set; } = "";
    public string Title { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
    public AssigneeRequest To { get; set; } = new();
}

public sealed class ActorRequest
{
    public string From { get; set; } = "";
}

public sealed class CompleteRequest
{
    public string From { get; set; } = "";
    public string Note { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
}

public static class WorkflowApi
{
    public static WebApplication UseWorkflow(this WebApplication app, Engine engine, IReadOnlyCollection<string> apiKeys)
    {
        var keys = apiKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToHashSet(StringComparer.Ordinal);

        app.Use(async (ctx, next) =>
        {
            if (keys.Count > 0 && !IsPublic(ctx.Request.Path))
            {
                var key = ctx.Request.Headers["X-API-Key"].ToString();
                if (string.IsNullOrEmpty(key))
                {
                    var auth = ctx.Request.Headers.Authorization.ToString();
                    if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        key = auth["Bearer ".Length..].Trim();
                }
                if (!keys.Contains(key))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new { error = EngineException.DefaultMessage(EngineErrorKind.Unauthorized) }, JsonConfig.Options);
                    return;
                }
            }

            using var tenant = TenantContext.Use(ctx.Request.Headers["X-Tenant-Id"].ToString());
            try
            {
                await next();
            }
            catch (EngineException ex)
            {
                ctx.Response.StatusCode = ErrorMapping.StatusCode(ex);
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message }, JsonConfig.Options);
            }
            catch (JsonException ex)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message }, JsonConfig.Options);
            }
        });

        MapSwagger(app);
        MapRoutes(app, engine);
        return app;
    }

    private static bool IsPublic(PathString path)
    {
        var p = path.Value ?? "";
        return p is "/" or "/health" or "/openapi.yaml" or "/swagger" or "/swagger/" or "/docs";
    }

    private static void MapSwagger(WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/swagger"));
        app.MapGet("/docs", () => Results.Redirect("/swagger"));
        app.MapGet("/openapi.yaml", () => Embedded("openapi.yaml", "application/yaml; charset=utf-8"));
        app.MapGet("/swagger", () => Embedded("swagger.html", "text/html; charset=utf-8"));
    }

    private static IResult Embedded(string fileName, string contentType)
    {
        var asm = typeof(WorkflowApi).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return Results.NotFound();
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return Results.Text(reader.ReadToEnd(), contentType);
    }

    private static void MapRoutes(WebApplication app, Engine engine)
    {
        app.MapGet("/health", () => Results.Json(new { status = "ok" }, JsonConfig.Options));

        app.MapPost("/v1/definitions", async (HttpRequest req) =>
        {
            var body = await ReadJson<RegisterDefinitionRequest>(req) ?? new();
            var outDef = await engine.Register(body.Key, body.Name, req.HttpContext.RequestAborted);
            return Results.Json(outDef, JsonConfig.Options, statusCode: StatusCodes.Status201Created);
        });

        app.MapGet("/v1/definitions/{key}", async (string key, CancellationToken ct) =>
        {
            var outDef = await engine.LatestDefinition(key, ct);
            return Results.Json(outDef, JsonConfig.Options);
        });

        app.MapPost("/v1/processes/start", async (HttpRequest req) =>
        {
            var body = await ReadJson<StartProcessRequest>(req) ?? new();
            if (body.Initiator.Length == 0)
                body.Initiator = Actor(req);
            var result = await engine.Start(body.ProcessKey, body.Initiator, body.Parameters, req.HttpContext.RequestAborted);
            return Results.Json(result, JsonConfig.Options, statusCode: StatusCodes.Status201Created);
        });

        app.MapGet("/v1/processes/{processKey}/instances", async (string processKey, CancellationToken ct) =>
        {
            var list = await engine.ListByProcessKey(processKey, ct);
            return Results.Json(list, JsonConfig.Options);
        });

        app.MapPost("/v1/referrals", async (HttpRequest req) =>
        {
            var body = await ReadJson<CreateReferralRequest>(req) ?? new();
            var from = body.From.Length > 0 ? body.From : Actor(req);
            var result = await engine.Refer(from, new ReferInput
            {
                DefinitionKey = body.DefinitionKey,
                ParentInstanceId = body.ParentInstanceId,
                Title = body.Title,
                Parameters = body.Parameters,
                ToKind = body.To.Kind,
                ToId = body.To.Id,
                ToIds = body.To.Ids,
            }, req.HttpContext.RequestAborted);
            return Results.Json(result, JsonConfig.Options, statusCode: StatusCodes.Status201Created);
        });

        app.MapGet("/v1/tasks", Pending);
        app.MapGet("/v1/inbox", Pending);

        async Task<IResult> Pending(HttpRequest req, CancellationToken ct)
        {
            var user = req.Query["user"].ToString();
            var group = req.Query["group"].ToString();
            if (user.Length == 0 && group.Length == 0)
                user = Actor(req);
            var tasks = await engine.PendingTasks(user, group, ct);
            return Results.Json(tasks, JsonConfig.Options);
        }

        app.MapGet("/v1/tasks/{id}", async (string id, CancellationToken ct) =>
        {
            var task = await engine.GetTask(id, ct);
            return Results.Json(task, JsonConfig.Options);
        });

        app.MapPost("/v1/tasks/{id}/claim", async (string id, HttpRequest req) =>
        {
            var task = await engine.ClaimTask(id, await DecodeActor(req), req.HttpContext.RequestAborted);
            return Results.Json(task, JsonConfig.Options);
        });

        app.MapPost("/v1/tasks/{id}/unclaim", async (string id, HttpRequest req) =>
        {
            var task = await engine.UnclaimTask(id, await DecodeActor(req), req.HttpContext.RequestAborted);
            return Results.Json(task, JsonConfig.Options);
        });

        app.MapPost("/v1/tasks/{id}/complete", async (string id, HttpRequest req) =>
        {
            var body = new CompleteRequest();
            if (req.ContentLength is > 0)
                body = await ReadJson<CompleteRequest>(req) ?? new();
            var who = body.From.Length > 0 ? body.From : Actor(req);
            var result = await engine.CompleteTask(id, who, body.Note, body.Parameters, req.HttpContext.RequestAborted);
            return Results.Json(result, JsonConfig.Options);
        });

        app.MapGet("/v1/instances/{id}", async (string id, CancellationToken ct) =>
        {
            var inst = await engine.GetInstance(id, ct);
            return Results.Json(inst, JsonConfig.Options);
        });

        app.MapGet("/v1/instances/{id}/tasks", async (string id, CancellationToken ct) =>
        {
            var tasks = await engine.ListTasksByInstance(id, ct);
            return Results.Json(tasks, JsonConfig.Options);
        });

        app.MapGet("/v1/instances/{id}/completion", async (string id, CancellationToken ct) =>
        {
            var comp = await engine.Completion(id, ct);
            return Results.Json(comp, JsonConfig.Options);
        });
    }

    private static string Actor(HttpRequest req) => req.Headers["X-Actor-Id"].ToString();

    private static async Task<string> DecodeActor(HttpRequest req)
    {
        var from = Actor(req);
        if (req.ContentLength is > 0)
        {
            var body = await ReadJson<ActorRequest>(req);
            if (body is not null && body.From.Length > 0)
                from = body.From;
        }
        return from;
    }

    private static async Task<T?> ReadJson<T>(HttpRequest req)
    {
        return await req.ReadFromJsonAsync<T>(JsonConfig.Options);
    }
}
