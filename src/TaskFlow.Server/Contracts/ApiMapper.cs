using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Server;

public static class ApiMapper
{
    public static DefinitionDto ToDto(this Definition d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        Key = d.Key,
        Name = d.Name,
        CreatedAt = d.CreatedAt,
    };

    public static TaskDto ToDto(this WorkflowTask t) => new()
    {
        Id = t.Id,
        TenantId = t.TenantId,
        InstanceId = t.InstanceId,
        ParentInstanceId = t.ParentInstanceId,
        DefinitionKey = t.DefinitionKey,
        Title = t.Title,
        AssigneeKind = t.AssigneeKind,
        AssigneeId = t.AssigneeId,
        AssignedBy = t.AssignedBy,
        ClaimedBy = t.ClaimedBy,
        Status = t.Status,
        Note = t.Note,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        CompletedAt = t.CompletedAt,
    };

    public static InstanceDto ToDto(this ProcessInstance i) => new()
    {
        Id = i.Id,
        TenantId = i.TenantId,
        DefinitionId = i.DefinitionId,
        DefinitionKey = i.DefinitionKey,
        ParentInstanceId = i.ParentInstanceId,
        Status = i.Status,
        Parameters = i.Parameters,
        Initiator = i.StartedBy,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt,
    };

    public static StartResultDto ToDto(this StartResult r) => new()
    {
        DefinitionKey = r.DefinitionKey,
        InstanceId = r.InstanceId,
    };

    public static ReferResultDto ToDto(this ReferResult r) => new()
    {
        InstanceId = r.InstanceId,
        DefinitionKey = r.DefinitionKey,
        Task = r.Task?.ToDto(),
        Tasks = r.Tasks.Select(t => t.ToDto()).ToList(),
    };

    public static CompletionDto ToDto(this Completion c) => new()
    {
        InstanceId = c.InstanceId,
        AllCompleted = c.AllCompleted,
        Total = c.Total,
        Completed = c.Completed,
        Open = c.Open,
        Tasks = c.Tasks.Select(t => t.ToDto()).ToList(),
    };

    public static CompleteResultDto ToDto(this CompleteResult r) => new()
    {
        Task = r.Task.ToDto(),
        Completion = r.Completion.ToDto(),
    };

    public static CompleteAndEndResultDto ToDto(this CompleteAndEndResult r) => new()
    {
        Task = r.Task.ToDto(),
        Completion = r.Completion.ToDto(),
        Process = r.Process.ToDto(),
        CancelledTasks = r.CancelledTasks,
    };

    public static ProcessInstanceDetailDto ToDto(this ProcessInstanceDetail i) => new()
    {
        InstanceId = i.InstanceId,
        ProcessKey = i.ProcessKey,
        DefinitionKey = i.DefinitionKey,
        Initiator = i.Initiator,
        Status = i.Status,
        Parameters = i.Parameters,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt,
        Tasks = i.Tasks.Select(t => t.ToDto()).ToList(),
        TaskTotal = i.TaskTotal,
        TasksCompleted = i.TasksCompleted,
        TasksOpen = i.TasksOpen,
        AllTasksCompleted = i.AllTasksCompleted,
    };

    public static ProcessListDto ToDto(this ProcessList list) => new()
    {
        ProcessKey = list.ProcessKey,
        Total = list.Total,
        Instances = list.Instances.Select(i => i.ToDto()).ToList(),
    };

    public static UserProcessListDto ToDto(this UserProcessList list) => new()
    {
        User = list.User,
        State = list.State,
        Open = list.Open,
        Closed = list.Closed,
        NotStarted = list.NotStarted,
        Total = list.Total,
        Instances = list.Instances.Select(i => i.ToDto()).ToList(),
    };
}
