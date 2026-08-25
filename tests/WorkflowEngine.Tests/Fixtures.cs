namespace WorkflowEngine.Tests;

internal static class Fixtures
{
    public static Engine NewEngine()
    {
        var dir = new StaticDirectory(
            ["alice", "mortenaho", "cara", "dan"],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["legal"] = ["mortenaho", "cara"],
                ["finance"] = ["dan", "cara"],
            });
        return new Engine(new MemoryStore(), dir);
    }
}
