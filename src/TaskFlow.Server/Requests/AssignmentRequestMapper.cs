using TaskFlow.Application;

namespace TaskFlow.Server;

internal static class AssignmentRequestMapper
{
    public static AssignToInput ToInput(CreateAssignmentRequest request) => new()
    {
        DefinitionKey = request.DefinitionKey,
        ParentInstanceId = request.ParentInstanceId,
        Title = request.Title,
        Parameters = request.Parameters,
        Join = request.Join,
        OnAllCompleted = request.OnAllCompleted is null ? null : ToInput(request.OnAllCompleted),
        ToKind = request.To.Kind,
        ToId = request.To.Id,
        ToIds = request.To.Ids,
    };
}
