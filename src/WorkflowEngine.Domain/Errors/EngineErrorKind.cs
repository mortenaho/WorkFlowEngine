namespace WorkflowEngine.Domain;

public enum EngineErrorKind
{
    NotFound,
    Forbidden,
    Invalid,
    NotOpen,
    AlreadyClaimed,
    NotClaimed,
    EmptyGroup,
    Unauthorized,
    ForbiddenTenant,
}
