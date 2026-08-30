using System.Text.Json.Serialization;
using TaskFlow.Application;

namespace TaskFlow.Server;

public static class WorkflowHosting
{
    public static IServiceCollection AddWorkflow(this IServiceCollection services, Engine engine, IReadOnlyCollection<string> apiKeys)
    {
        services.AddSingleton(engine);
        services.AddSingleton(new ApiKeySettings(apiKeys));
        services.AddControllers()
            .AddApplicationPart(typeof(WorkflowHosting).Assembly)
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonConfig.Options.PropertyNamingPolicy;
                o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                o.JsonSerializerOptions.TypeInfoResolver = JsonConfig.Options.TypeInfoResolver;
            });
        return services;
    }

    public static WebApplication UseWorkflow(this WebApplication app)
    {
        app.UseMiddleware<WorkflowPipelineMiddleware>();
        app.MapControllers();
        return app;
    }
}
