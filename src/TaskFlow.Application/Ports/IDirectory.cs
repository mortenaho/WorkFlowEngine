namespace TaskFlow.Application;

public interface IDirectory
{
    /// <summary>
    /// When false, the engine accepts any user/group id without looking up a directory
    /// (typical for host apps that already authenticate and only pass opaque ids like "102").
    /// </summary>
    bool EnforcesMembership { get; }

    Task<bool> UserExists(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GroupMembers(string groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> UserGroups(string userId, CancellationToken cancellationToken = default);
    Task<bool> IsMember(string userId, string groupId, CancellationToken cancellationToken = default);
}
