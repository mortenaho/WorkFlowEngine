using WorkflowEngine.Application;
using WorkflowEngine.Domain;
using WorkflowEngine.Infrastructure;

var dir = new StaticDirectory(
    ["alice", "bob", "cara", "dan"],
    new Dictionary<string, IReadOnlyList<string>>
    {
        ["legal"] = ["bob", "cara"],
        ["finance"] = ["dan", "cara"],
    });
var eng = new Engine(new MemoryStore(), dir);

var started = await eng.Start("purchase", "alice", new Dictionary<string, object?> { ["amount"] = 1.5e8 });
Console.WriteLine($"start {started.DefinitionKey} {started.InstanceId}");

var refer = await eng.Refer("alice", new ReferInput
{
    DefinitionKey = started.DefinitionKey,
    ParentInstanceId = started.InstanceId,
    Title = "تأیید موازی",
    ToKind = AssigneeKind.Users,
    ToIds = ["bob", "cara"],
});
Console.WriteLine($"refer {refer.InstanceId} tasks={refer.Tasks.Count}");

foreach (var task in refer.Tasks)
{
    var done = await eng.CompleteTask(task.Id, task.AssigneeId, "تأیید شد");
    Console.WriteLine($"complete {task.AssigneeId} allCompleted={done.Completion.AllCompleted}");
}

var group = await eng.Refer("alice", new ReferInput
{
    DefinitionKey = started.DefinitionKey,
    ParentInstanceId = started.InstanceId,
    Title = "بررسی حقوقی",
    ToKind = AssigneeKind.Group,
    ToId = "legal",
});
await eng.ClaimTask(group.Task!.Id, "bob");
var last = await eng.CompleteTask(group.Task.Id, "bob", "ok");
Console.WriteLine($"group allCompleted={last.Completion.AllCompleted}");
