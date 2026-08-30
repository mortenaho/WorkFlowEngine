using TaskFlow.Application;
using TaskFlow.Infrastructure;

namespace TaskFlow.Server;

internal static class AppComposition
{
    public static void ConfigureHost(WebApplicationBuilder builder)
    {
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

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
            return;

        builder.WebHost.UseUrls(ResolveListenUrls(Env("ADDR", ":8081")));
    }

    public static IReadOnlyList<string> ReadApiKeys() => SplitCsv(Env("WF_API_KEYS", ""));

    public static IDirectory CreateDirectory()
    {
        var users = SplitCsv(Env("WF_USERS", ""));
        var groups = ReadGroupsFromEnv();
        return users.Count == 0 && groups.Count == 0
            ? new OpenDirectory()
            : new StaticDirectory(users, groups);
    }

    public static async Task<StoreLifetime> CreateStoreAsync()
    {
        var dsn = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(dsn))
            return new StoreLifetime(new MemoryStore());

        return new StoreLifetime(await OpenPostgresWithRetry(dsn));
    }

    public static string ListenAddress =>
        Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
        ?? Environment.GetEnvironmentVariable("ADDR")
        ?? ":8081";

    public static void LogStartup(IDirectory directory, IStore store, IReadOnlyList<string> apiKeys)
    {
        Console.WriteLine(directory is OpenDirectory
            ? "directory: open (any user/group id accepted)"
            : "directory: static");
        Console.WriteLine(store is PostgresStore ? "store: postgres" : "store: memory");
        Console.WriteLine($"workflow engine listening on {ListenAddress}");
        Console.WriteLine(apiKeys.Count > 0
            ? $"api auth: X-API-Key required ({apiKeys.Count} key(s))"
            : "api auth: disabled (Development)");
        Console.WriteLine("swagger UI: http://localhost:8081/swagger");
    }

    private static string ResolveListenUrls(string addr)
    {
        if (addr.StartsWith(':'))
            return "http://0.0.0.0" + addr;
        if (addr.Contains("://", StringComparison.Ordinal))
            return addr;
        return "http://" + addr;
    }

    private static Dictionary<string, IReadOnlyList<string>> ReadGroupsFromEnv()
    {
        const string prefix = "WF_GROUP_";
        var groups = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string key
                || !key.StartsWith(prefix, StringComparison.Ordinal)
                || key.Length == prefix.Length)
                continue;
            groups[key[prefix.Length..].ToLowerInvariant()] = SplitCsv(entry.Value?.ToString() ?? "");
        }
        return groups;
    }

    private static async Task<PostgresStore> OpenPostgresWithRetry(string dsn)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 30; attempt++)
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

    private static string Env(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static IReadOnlyList<string> SplitCsv(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

internal sealed class StoreLifetime : IAsyncDisposable
{
    private readonly IAsyncDisposable? _disposable;

    public IStore Store { get; }

    public StoreLifetime(IStore store)
    {
        Store = store;
        _disposable = store as IAsyncDisposable;
    }

    public ValueTask DisposeAsync() =>
        _disposable?.DisposeAsync() ?? ValueTask.CompletedTask;
}
