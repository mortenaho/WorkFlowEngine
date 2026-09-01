using TaskFlow.Server;

namespace TaskFlow.Tests;

public class ServerMappingTests
{
    [Theory]
    [InlineData(EngineErrorKind.NotFound, 404)]
    [InlineData(EngineErrorKind.Forbidden, 403)]
    [InlineData(EngineErrorKind.ForbiddenTenant, 403)]
    [InlineData(EngineErrorKind.Unauthorized, 401)]
    [InlineData(EngineErrorKind.Invalid, 400)]
    [InlineData(EngineErrorKind.NotOpen, 400)]
    [InlineData(EngineErrorKind.EmptyGroup, 400)]
    [InlineData(EngineErrorKind.NotClaimed, 400)]
    [InlineData(EngineErrorKind.AlreadyClaimed, 409)]
    public void ErrorMappingStatusCodes(EngineErrorKind kind, int expected)
    {
        var code = ErrorMapping.StatusCode(new EngineException(kind));
        Assert.Equal(expected, code);
    }

    [Fact]
    public void ApiMapperMapsEntitiesToDtos()
    {
        var now = DateTime.UtcNow;
        var task = new WorkflowTask
        {
            Id = "t1",
            TenantId = "acme",
            InstanceId = "i1",
            ParentInstanceId = "root",
            DefinitionKey = "purchase",
            Title = "بررسی",
            AssigneeKind = AssigneeKind.User,
            AssigneeId = "mortenaho",
            AssignedBy = "sara",
            Status = TaskStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var dto = task.ToDto();
        Assert.Equal("t1", dto.Id);
        Assert.Equal("acme", dto.TenantId);
        Assert.Equal("بررسی", dto.Title);
        Assert.Equal(AssigneeKind.User, dto.AssigneeKind);

        var inst = new ProcessInstance
        {
            Id = "i1",
            TenantId = "acme",
            DefinitionKey = "purchase",
            StartedBy = "sara",
            Status = InstanceStatus.Running,
            Parameters = new Dictionary<string, object?> { ["amount"] = 10 },
            CreatedAt = now,
            UpdatedAt = now,
        };

        var instDto = inst.ToDto();
        Assert.Equal("sara", instDto.Initiator);
        Assert.Equal(10, instDto.Parameters!["amount"]);

        var start = new StartResult { DefinitionKey = "purchase", InstanceId = "i1" };
        var startDto = start.ToDto();
        Assert.Equal("purchase", startDto.DefinitionKey);
        Assert.Equal("i1", startDto.InstanceId);

        var assign = new AssignToResult
        {
            InstanceId = "child",
            DefinitionKey = "purchase",
            Task = task,
            Tasks = [task],
        };
        var assignDto = assign.ToDto();
        Assert.Equal("child", assignDto.InstanceId);
        Assert.NotNull(assignDto.Task);
        Assert.Single(assignDto.Tasks);

        var completion = new Completion
        {
            InstanceId = "child",
            AllCompleted = false,
            Total = 2,
            Completed = 1,
            Open = 1,
            Tasks = [task],
        };
        var compDto = completion.ToDto();
        Assert.False(compDto.AllCompleted);
        Assert.Equal(2, compDto.Total);

        var complete = new CompleteResult
        {
            Task = task,
            Completion = completion,
            Next = assign,
        };
        var completeDto = complete.ToDto();
        Assert.NotNull(completeDto.Next);

        var detail = new ProcessInstanceDetail
        {
            InstanceId = "i1",
            ProcessKey = "purchase",
            DefinitionKey = "purchase",
            Initiator = "sara",
            Status = InstanceStatus.Running,
            Tasks = [task],
            TaskTotal = 1,
            TasksOpen = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var detailDto = detail.ToDto();
        Assert.Equal(1, detailDto.TaskTotal);
        Assert.Single(detailDto.Tasks);

        var userList = new UserProcessList
        {
            User = "sara",
            State = ProcessState.Open,
            Open = 1,
            Closed = 0,
            NotStarted = 0,
            Total = 1,
            Instances = [detail],
        };
        var userDto = userList.ToDto();
        Assert.Equal("sara", userDto.User);
        Assert.Equal(ProcessState.Open, userDto.State);
    }
}
