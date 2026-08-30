namespace TaskFlow.Application;

public sealed class AmbientTenantProvider : ITenantProvider
{
    public string Current => TenantContext.Id;
}
