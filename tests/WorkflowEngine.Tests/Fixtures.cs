using WorkflowEngine;

namespace WorkflowEngine.Tests;

internal static class Fixtures
{
    public static Engine NewEngine()
    {
        var dir = new StaticDirectory(
            ["alice", "bob", "cara", "dan"],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["legal"] = ["bob", "cara"],
                ["finance"] = ["dan", "cara"],
            });
        return new Engine(new MemoryStore(), dir);
    }
}
