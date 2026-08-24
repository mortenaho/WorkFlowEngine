using WorkflowEngine;
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

var directory = new StaticDirectory(
    SplitCsv(Env("WF_USERS", "alice,bob,cara,dan,manager,ceo")),
    new Dictionary<string, IReadOnlyList<string>>
    {
        ["legal"] = SplitCsv(Env("WF_GROUP_LEGAL", "bob,cara")),
        ["finance"] = SplitCsv(Env("WF_GROUP_FINANCE", "dan,cara")),
    });

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

var app = builder.Build();
app.UseWorkflow(engine, apiKeys);

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
