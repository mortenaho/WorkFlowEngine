namespace WorkflowEngine.Domain;

public sealed class EngineException : Exception
{
    public EngineErrorKind Kind { get; }

    public EngineException(EngineErrorKind kind, string? detail = null)
        : base(Format(kind, detail))
    {
        Kind = kind;
    }

    public static EngineException NotFound(string? detail = null) => new(EngineErrorKind.NotFound, detail);
    public static EngineException Invalid(string detail) => new(EngineErrorKind.Invalid, detail);
    public static EngineException Forbidden() => new(EngineErrorKind.Forbidden);
    public static EngineException ForbiddenTenant() => new(EngineErrorKind.ForbiddenTenant);
    public static EngineException NotOpen() => new(EngineErrorKind.NotOpen);
    public static EngineException AlreadyClaimed() => new(EngineErrorKind.AlreadyClaimed);
    public static EngineException NotClaimed() => new(EngineErrorKind.NotClaimed);
    public static EngineException EmptyGroup() => new(EngineErrorKind.EmptyGroup);
    public static EngineException Unauthorized() => new(EngineErrorKind.Unauthorized);

    public static string DefaultMessage(EngineErrorKind kind) => kind switch
    {
        EngineErrorKind.NotFound => "not found",
        EngineErrorKind.Forbidden => "forbidden",
        EngineErrorKind.Invalid => "invalid",
        EngineErrorKind.NotOpen => "task is not open",
        EngineErrorKind.AlreadyClaimed => "task already claimed",
        EngineErrorKind.NotClaimed => "task is not claimed",
        EngineErrorKind.EmptyGroup => "group has no members",
        EngineErrorKind.Unauthorized => "unauthorized",
        EngineErrorKind.ForbiddenTenant => "tenant mismatch",
        _ => "error",
    };

    private static string Format(EngineErrorKind kind, string? detail)
    {
        var msg = DefaultMessage(kind);
        return string.IsNullOrEmpty(detail) ? msg : $"{msg}: {detail}";
    }
}
