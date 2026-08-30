using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TaskFlow.Client;

public static class TaskFlowServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TaskFlowClient"/> (typed HttpClient) and <see cref="TaskFlowOrchestrator"/>
    /// pointed at an already-running TaskFlow.Server.
    /// </summary>
    public static IHttpClientBuilder AddTaskFlowClient(
        this IServiceCollection services,
        Action<TaskFlowClientOptions> configure)
    {
        services.AddOptions<TaskFlowClientOptions>().Configure(configure);
        services.AddTransient<TaskFlowOrchestrator>();
        return services.AddHttpClient<TaskFlowClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<TaskFlowClientOptions>>().Value;
            http.BaseAddress = options.BaseAddress;
            if (!string.IsNullOrEmpty(options.ApiKey))
                http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", options.ApiKey);
            if (!string.IsNullOrEmpty(options.TenantId))
                http.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", options.TenantId);
        });
    }

    public static IHttpClientBuilder AddTaskFlowClient(
        this IServiceCollection services,
        Uri baseAddress,
        string? apiKey = null,
        string? tenantId = null)
        => services.AddTaskFlowClient(o =>
        {
            o.BaseAddress = baseAddress;
            o.ApiKey = apiKey;
            o.TenantId = tenantId;
        });
}
