using WorkflowEngine.Application;
using WorkflowEngine.Server;

var builder = WebApplication.CreateBuilder(args);
AppComposition.ConfigureHost(builder);

var apiKeys = AppComposition.ReadApiKeys();
ApiKeyStartup.EnsureConfigured(builder.Environment.EnvironmentName, apiKeys);

var directory = AppComposition.CreateDirectory();
await using var storeLifetime = await AppComposition.CreateStoreAsync();
var engine = new Engine(storeLifetime.Store, directory);
builder.Services.AddWorkflow(engine, apiKeys);

var app = builder.Build();
app.UseWorkflow();
AppComposition.LogStartup(directory, storeLifetime.Store, apiKeys);
await app.RunAsync();

public partial class Program;
