namespace WorkflowEngine.Infrastructure;

public sealed class StaticDirectory : IDirectory
{
    private readonly HashSet<string> _users;
    private readonly Dictionary<string, string[]> _groups;

    public StaticDirectory(IEnumerable<string> users, IReadOnlyDictionary<string, IReadOnlyList<string>> groups)
    {
        _users = new HashSet<string>(users);
        _groups = groups.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);
    }

    public Task<bool> UserExists(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.Contains(userId));

    public Task<IReadOnlyList<string>> GroupMembers(string groupId, CancellationToken cancellationToken = default)
    {
        if (!_groups.TryGetValue(groupId, out var members))
            return Task.FromResult<IReadOnlyList<string>>([]);
        return Task.FromResult<IReadOnlyList<string>>(members.ToArray());
    }

    public Task<IReadOnlyList<string>> UserGroups(string userId, CancellationToken cancellationToken = default)
    {
        var outList = new List<string>();
        foreach (var (gid, members) in _groups)
        {
            if (members.Contains(userId))
                outList.Add(gid);
        }
        return Task.FromResult<IReadOnlyList<string>>(outList);
    }
}
