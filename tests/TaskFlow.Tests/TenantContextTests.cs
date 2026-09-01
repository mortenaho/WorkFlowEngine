namespace TaskFlow.Tests;

public class TenantContextTests
{
    [Fact]
    public void UseSetsAndRestoresTenant()
    {
        Assert.Equal(Tenant.Default, TenantContext.Id);

        using (TenantContext.Use("acme"))
        {
            Assert.Equal("acme", TenantContext.Id);
            using (TenantContext.Use("other"))
                Assert.Equal("other", TenantContext.Id);
            Assert.Equal("acme", TenantContext.Id);
        }

        Assert.Equal(Tenant.Default, TenantContext.Id);
    }

    [Fact]
    public void UseNormalizesEmptyToDefault()
    {
        using (TenantContext.Use(""))
            Assert.Equal(Tenant.Default, TenantContext.Id);
    }

    [Fact]
    public async Task AmbientTenantFlowsThroughEngine()
    {
        var eng = Fixtures.NewEngine();
        string acmeId;
        using (TenantContext.Use("acme"))
        {
            var started = await eng.Start("purchase", "sara");
            acmeId = started.InstanceId;
        }

        using (TenantContext.Use("other"))
        {
            var ex = await Assert.ThrowsAsync<EngineException>(() => eng.GetInstance(acmeId));
            Assert.Equal(EngineErrorKind.ForbiddenTenant, ex.Kind);
        }

        using (TenantContext.Use("acme"))
        {
            var inst = await eng.GetInstance(acmeId);
            Assert.Equal("sara", inst.StartedBy);
        }
    }
}
