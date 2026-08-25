using System.Text.Json;
using WorkflowEngine.Application;
using WorkflowEngine.Domain;

namespace WorkflowEngine.Server;

public sealed class ApiKeySettings(IEnumerable<string> keys)
{
    public HashSet<string> Keys { get; } = keys
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Select(k => k.Trim())
        .ToHashSet(StringComparer.Ordinal);
}

public sealed class WorkflowPipelineMiddleware(RequestDelegate next, ApiKeySettings apiKeys)
{
    public async Task Invoke(HttpContext ctx)
    {
        if (apiKeys.Keys.Count > 0 && !IsPublic(ctx.Request.Path))
        {
            var key = ctx.Request.Headers["X-API-Key"].ToString();
            if (string.IsNullOrEmpty(key))
            {
                var auth = ctx.Request.Headers.Authorization.ToString();
                if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    key = auth["Bearer ".Length..].Trim();
            }
            if (!apiKeys.Keys.Contains(key))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = EngineException.DefaultMessage(EngineErrorKind.Unauthorized) }, JsonConfig.Options);
                return;
            }
        }

        using var tenant = TenantContext.Use(ctx.Request.Headers["X-Tenant-Id"].ToString());
        try
        {
            await next(ctx);
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
    }

    private static bool IsPublic(PathString path)
    {
        var p = path.Value ?? "";
        return p is "/" or "/health" or "/openapi.yaml" or "/swagger" or "/swagger/" or "/docs";
    }
}
