namespace WorkflowEngine.Application;

public interface ITenantProvider
{
    string Current { get; }
}
