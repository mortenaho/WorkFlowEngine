using TaskFlow.Application;
using TaskFlow.Domain;
using TaskFlow.Infrastructure;

var dir = new StaticDirectory(
    ["sara", "mortenaho", "tina", "hamid"],
    new Dictionary<string, IReadOnlyList<string>>
    {
        ["legal"] = ["mortenaho", "tina"],
        ["finance"] = ["hamid", "tina"],
    });
var eng = new Engine(new MemoryStore(), dir);

var started = await eng.Start("purchase", "sara", new Dictionary<string, object?> { ["amount"] = 1.5e8 });
Console.WriteLine($"start {started.DefinitionKey} {started.InstanceId}");

var parallel = await eng.AssignTo("sara", new AssignToInput
{
    DefinitionKey = started.DefinitionKey,
    ParentInstanceId = started.InstanceId,
    Title = "تأیید موازی",
    ToKind = AssigneeKind.Users,
    ToIds = ["mortenaho", "tina"],
    OnAllCompleted = new AssignToInput
    {
        Title = "بررسی حقوقی",
        ToKind = AssigneeKind.Group,
        ToId = "legal",
    },
});
Console.WriteLine($"parallel {parallel.InstanceId} tasks={parallel.Tasks.Count}");

foreach (var task in parallel.Tasks)
{
    await eng.CompleteTask(task.Id, task.AssigneeId, "تأیید شد");
    Console.WriteLine($"complete {task.AssigneeId}");
}

var legalTask = (await eng.PendingTasks("", "legal"))
    .FirstOrDefault(t => t.ParentInstanceId == started.InstanceId)
    ?? throw new InvalidOperationException("expected auto-advance to legal after parallel join");

await eng.ClaimTask(legalTask.Id, "mortenaho");
var last = await eng.CompleteTask(legalTask.Id, "mortenaho", "ok");
Console.WriteLine($"legal allCompleted={last.Completion.AllCompleted}");
