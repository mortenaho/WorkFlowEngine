namespace WorkflowEngine.Application;

public static class DirectoryExtensions
{
    public static async Task<bool> IsMember(this IDirectory directory, string userId, string groupId, CancellationToken cancellationToken = default)
    {
        var members = await directory.GroupMembers(groupId, cancellationToken);
        return members.Contains(userId);
    }
}
