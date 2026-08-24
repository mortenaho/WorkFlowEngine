using System.Text.Json.Serialization;

namespace WorkflowEngine;

public static class AssigneeKind
{
    public const string User = "user";
    public const string Group = "group";
    public const string Users = "users";
}

public static class InstanceStatus
{
    public const string Running = "running";
    public const string Completed = "completed";
}

public static class TaskStatus
{
    public const string Open = "open";
    public const string Claimed = "claimed";
    public const string Done = "done";
}

public sealed class Definition
{
    public string Id { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string TenantId { get; set; } = "";
    public string Key { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public Definition Clone() => new()
    {
        Id = Id,
        TenantId = TenantId,
        Key = Key,
        Name = Name,
        CreatedAt = CreatedAt,
    };
}

public sealed class ProcessInstance
{
    public string Id { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string TenantId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string ParentInstanceId { get; set; } = "";
    public string Status { get; set; } = InstanceStatus.Running;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Parameters { get; set; }
    [JsonPropertyName("initiator")]
    public string StartedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ProcessInstance Clone() => new()
    {
        Id = Id,
        TenantId = TenantId,
        DefinitionId = DefinitionId,
        DefinitionKey = DefinitionKey,
        ParentInstanceId = ParentInstanceId,
        Status = Status,
        Parameters = VarsUtil.Clone(Parameters),
        StartedBy = StartedBy,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
    };
}

public sealed class WorkflowTask
{
    public string Id { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string TenantId { get; set; } = "";
    public string InstanceId { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string ParentInstanceId { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string DefinitionKey { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Title { get; set; } = "";
    public string AssigneeKind { get; set; } = "";
    public string AssigneeId { get; set; } = "";
    public string AssignedBy { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string ClaimedBy { get; set; } = "";
    public string Status { get; set; } = TaskStatus.Open;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CompletedAt { get; set; }

    public WorkflowTask Clone()
    {
        var cp = new WorkflowTask
        {
            Id = Id,
            TenantId = TenantId,
            InstanceId = InstanceId,
            ParentInstanceId = ParentInstanceId,
            DefinitionKey = DefinitionKey,
            Title = Title,
            AssigneeKind = AssigneeKind,
            AssigneeId = AssigneeId,
            AssignedBy = AssignedBy,
            ClaimedBy = ClaimedBy,
            Status = Status,
            Note = Note,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
        if (CompletedAt is { } ts)
            cp.CompletedAt = ts;
        return cp;
    }
}

public sealed class TaskFilter
{
    public string? UserId { get; set; }
    public string? GroupId { get; set; }
    public IReadOnlyList<string>? GroupIds { get; set; }
    public string? InstanceId { get; set; }
    public string? Status { get; set; }
    public IReadOnlyList<string>? Statuses { get; set; }
    public string? ClaimedBy { get; set; }
    public string? TenantId { get; set; }
}

public sealed class StartResult
{
    public string DefinitionKey { get; set; } = "";
    public string InstanceId { get; set; } = "";
}

public sealed class ReferInput
{
    public string DefinitionKey { get; set; } = "";
    public string ParentInstanceId { get; set; } = "";
    public string Title { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
    public string ToKind { get; set; } = "";
    public string ToId { get; set; } = "";
    public IReadOnlyList<string>? ToIds { get; set; }
}

public sealed class ReferResult
{
    public string InstanceId { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowTask? Task { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = [];
}

public sealed class Completion
{
    public string InstanceId { get; set; } = "";
    public bool AllCompleted { get; set; }
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Open { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = [];
}

public sealed class CompleteResult
{
    public WorkflowTask Task { get; set; } = new();
    public Completion Completion { get; set; } = new();
}

public sealed class ProcessInstanceDetail
{
    public string InstanceId { get; set; } = "";
    public string ProcessKey { get; set; } = "";
    public string DefinitionKey { get; set; } = "";
    public string Initiator { get; set; } = "";
    public string Status { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Parameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = [];
    public int TaskTotal { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksOpen { get; set; }
    public bool AllTasksCompleted { get; set; }
}

public sealed class ProcessList
{
    public string ProcessKey { get; set; } = "";
    public int Total { get; set; }
    public List<ProcessInstanceDetail> Instances { get; set; } = [];
}

public static class VarsUtil
{
    public static Dictionary<string, object?> Clone(Dictionary<string, object?>? v)
    {
        if (v is null || v.Count == 0)
            return [];
        return new Dictionary<string, object?>(v);
    }

    public static Dictionary<string, object?> Merge(Dictionary<string, object?>? v, Dictionary<string, object?>? other)
    {
        var outDict = Clone(v);
        if (other is null)
            return outDict;
        foreach (var (k, val) in other)
            outDict[k] = val;
        return outDict;
    }
}
