namespace WorkflowEngine.Infrastructure;

/// <summary>
/// Passthrough directory: any non-empty user or group id is accepted.
/// Use when the host app owns identity and only sends opaque ids (e.g. X-Actor-Id: 102).
/// </summary>
public sealed class OpenDirectory : IDirectory
{
    public bool EnforcesMembership => false;

    public Task<bool> UserExists(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(userId.Trim().Length > 0);

    public Task<IReadOnlyList<string>> GroupMembers(string groupId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> UserGroups(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<bool> IsMember(string userId, string groupId, CancellationToken cancellationToken = default)
        => Task.FromResult(userId.Trim().Length > 0 && groupId.Trim().Length > 0);
}
