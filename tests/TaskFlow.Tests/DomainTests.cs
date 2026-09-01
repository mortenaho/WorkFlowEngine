namespace TaskFlow.Tests;

public class DomainTests
{
    [Theory]
    [InlineData(null, Tenant.Default)]
    [InlineData("", Tenant.Default)]
    [InlineData("acme", "acme")]
    public void TenantNormalize(string? input, string expected)
    {
        Assert.Equal(expected, Tenant.Normalize(input));
    }

    [Fact]
    public void IdsNewProducesUniqueLowercaseHex()
    {
        var a = Ids.New();
        var b = Ids.New();
        Assert.NotEqual(a, b);
        Assert.Equal(32, a.Length);
        Assert.Matches("^[0-9a-f]{32}$", a);
    }

    [Theory]
    [InlineData(EngineErrorKind.NotFound, "not found")]
    [InlineData(EngineErrorKind.Forbidden, "forbidden")]
    [InlineData(EngineErrorKind.Invalid, "invalid")]
    [InlineData(EngineErrorKind.NotOpen, "task is not open")]
    [InlineData(EngineErrorKind.AlreadyClaimed, "task already claimed")]
    [InlineData(EngineErrorKind.NotClaimed, "task is not claimed")]
    [InlineData(EngineErrorKind.EmptyGroup, "group has no members")]
    [InlineData(EngineErrorKind.Unauthorized, "unauthorized")]
    [InlineData(EngineErrorKind.ForbiddenTenant, "tenant mismatch")]
    public void EngineExceptionDefaultMessage(EngineErrorKind kind, string expected)
    {
        Assert.Equal(expected, EngineException.DefaultMessage(kind));
    }

    [Fact]
    public void EngineExceptionIncludesDetail()
    {
        var ex = EngineException.Invalid("missing key");
        Assert.Equal(EngineErrorKind.Invalid, ex.Kind);
        Assert.Equal("invalid: missing key", ex.Message);
    }

    [Fact]
    public void EntityCloneIsIndependent()
    {
        var inst = new ProcessInstance
        {
            Id = "i1",
            Parameters = new Dictionary<string, object?> { ["x"] = 1 },
        };
        var copy = inst.Clone();
        copy.Parameters!["x"] = 2;
        Assert.Equal(1, inst.Parameters["x"]);
    }

    [Fact]
    public void WorkflowTaskClonePreservesCompletedAt()
    {
        var done = DateTime.UtcNow;
        var task = new WorkflowTask { Id = "t1", CompletedAt = done };
        var copy = task.Clone();
        Assert.Equal(done, copy.CompletedAt);
    }
}
