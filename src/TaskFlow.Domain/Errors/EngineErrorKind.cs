namespace TaskFlow.Domain;

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
