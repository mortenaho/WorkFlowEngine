using TaskFlow.Domain;

namespace TaskFlow.Server;

public static class ErrorMapping
{
    public static int StatusCode(EngineException ex) => ex.Kind switch
    {
        EngineErrorKind.NotFound => StatusCodes.Status404NotFound,
        EngineErrorKind.Forbidden or EngineErrorKind.ForbiddenTenant => StatusCodes.Status403Forbidden,
        EngineErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        EngineErrorKind.Invalid or EngineErrorKind.NotOpen or EngineErrorKind.EmptyGroup or EngineErrorKind.NotClaimed
            => StatusCodes.Status400BadRequest,
        EngineErrorKind.AlreadyClaimed => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}
