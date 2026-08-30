using Microsoft.Extensions.Hosting;

namespace TaskFlow.Server;

public static class ApiKeyStartup
{
    public const string MissingKeysMessage =
        "WF_API_KEYS is required when ASPNETCORE_ENVIRONMENT is not Development. " +
        "Set a comma-separated list of keys and send them as X-API-Key (or Authorization: Bearer). " +
        "This engine does not issue login tokens; the key is a shared secret for your backend or gateway. " +
        "For local use without keys, run with ASPNETCORE_ENVIRONMENT=Development.";

    public static void EnsureConfigured(string environmentName, IReadOnlyCollection<string> apiKeys)
    {
        if (Environments.Development.Equals(environmentName, StringComparison.OrdinalIgnoreCase))
            return;
        if (apiKeys.Count == 0)
            throw new InvalidOperationException(MissingKeysMessage);
    }
}
