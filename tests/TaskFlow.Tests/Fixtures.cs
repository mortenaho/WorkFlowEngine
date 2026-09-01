namespace TaskFlow.Tests;

internal static class Fixtures
{
    public static Engine NewEngine()
    {
        var dir = new StaticDirectory(
            ["sara", "mortenaho", "tina", "hamid"],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["legal"] = ["mortenaho", "tina"],
                ["finance"] = ["hamid", "tina"],
            });
        return new Engine(new MemoryStore(), dir);
    }
}
