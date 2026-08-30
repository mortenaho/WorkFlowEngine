namespace TaskFlow.Application;

public interface ITenantProvider
{
    string Current { get; }
}
