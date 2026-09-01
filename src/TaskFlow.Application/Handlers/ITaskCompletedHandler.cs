namespace TaskFlow.Application;

public interface ITaskCompletedHandler
{
    Task<AssignToResult?> HandleAsync(TaskCompletedEvent e, CancellationToken cancellationToken = default);
}
