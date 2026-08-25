using WorkflowEngine.Server;

namespace WorkflowEngine.Tests;

public class ApiKeyStartupTests
{
    [Fact]
    public void DevelopmentAllowsEmptyKeys()
    {
        ApiKeyStartup.EnsureConfigured("Development", []);
        ApiKeyStartup.EnsureConfigured("development", []);
    }

    [Fact]
    public void ProductionRequiresKeys()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ApiKeyStartup.EnsureConfigured("Production", []));
        Assert.Contains("WF_API_KEYS", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StagingRequiresKeys()
    {
        Assert.Throws<InvalidOperationException>(
            () => ApiKeyStartup.EnsureConfigured("Staging", []));
    }

    [Fact]
    public void ProductionAcceptsConfiguredKeys()
    {
        ApiKeyStartup.EnsureConfigured("Production", ["secret"]);
    }
}
