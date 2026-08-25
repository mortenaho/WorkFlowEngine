using WorkflowEngine.Application;
using WorkflowEngine.Infrastructure;
using WorkflowEngine.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonConfig.Options.PropertyNamingPolicy;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
    o.SerializerOptions.DefaultIgnoreCondition = JsonConfig.Options.DefaultIgnoreCondition;
});

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var addr = Environment.GetEnvironmentVariable("ADDR") ?? ":8081";
    var urls = addr.StartsWith(':')
        ? "http://0.0.0.0" + addr
        : addr.Contains("://", StringComparison.Ordinal)
            ? addr
            : "http://" + addr;
    builder.WebHost.UseUrls(urls);
}

var directory = new StaticDirectory(SplitCsv(Env("WF_USERS", "")), GroupsFromEnv());

IStore store = new MemoryStore();
PostgresStore? postgres = null;
var dsn = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrWhiteSpace(dsn))
{
    postgres = await OpenPostgres(dsn);
    store = postgres;
    Console.WriteLine("store: postgres");
}
else
{
    Console.WriteLine("store: memory");
}

var engine = new Engine(store, directory);
var apiKeys = SplitCsv(Env("WF_API_KEYS", ""));
builder.Services.AddWorkflow(engine, apiKeys);

var app = builder.Build();
app.UseWorkflow();

var listen = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
             ?? Environment.GetEnvironmentVariable("ADDR")
             ?? ":8081";
Console.WriteLine($"workflow engine listening on {listen}");
Console.WriteLine("swagger UI: http://localhost:8081/swagger");

try
{
    await app.RunAsync();
}
finally
{
    if (postgres is not null)
        await postgres.DisposeAsync();
}

static string Env(string key, string fallback)
{
    var v = Environment.GetEnvironmentVariable(key);
    return string.IsNullOrEmpty(v) ? fallback : v;
}

static IReadOnlyList<string> SplitCsv(string s) =>
    s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

static Dictionary<string, IReadOnlyList<string>> GroupsFromEnv()
{
    const string prefix = "WF_GROUP_";
    var groups = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
    var variables = Environment.GetEnvironmentVariables();
    foreach (var keyObj in variables.Keys)
    {
        if (keyObj is not string key
            || !key.StartsWith(prefix, StringComparison.Ordinal)
            || key.Length == prefix.Length)
            continue;
        var id = key[prefix.Length..].ToLowerInvariant();
        groups[id] = SplitCsv(variables[keyObj]?.ToString() ?? "");
    }
    return groups;
}

static async Task<PostgresStore> OpenPostgres(string dsn)
{
    Exception? last = null;
    for (var i = 0; i < 30; i++)
    {
        try
        {
            return await PostgresStore.Open(dsn);
        }
        catch (Exception ex)
        {
            last = ex;
            Console.WriteLine($"waiting for postgres: {ex.Message}");
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
    throw last ?? new InvalidOperationException("postgres unavailable");
}

public partial class Program;
