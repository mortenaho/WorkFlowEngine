using TaskFlow.Application;
using TaskFlow.Domain;
using TaskFlow.Infrastructure;

var dir = new StaticDirectory(
    ["alice", "mortenaho", "cara", "dan"],
    new Dictionary<string, IReadOnlyList<string>>
    {
        ["legal"] = ["mortenaho", "cara"],
        ["finance"] = ["dan", "cara"],
    });
var eng = new Engine(new MemoryStore(), dir);
var orch = new ProcessOrchestrator(eng);

var started = await eng.Start("purchase", "alice", new Dictionary<string, object?> { ["amount"] = 1.5e8 });
Console.WriteLine($"start {started.DefinitionKey} {started.InstanceId}");

var parallel = await eng.Refer("alice", new ReferInput
{
    DefinitionKey = started.DefinitionKey,
    ParentInstanceId = started.InstanceId,
    Title = "تأیید موازی",
    ToKind = AssigneeKind.Users,
    ToIds = ["mortenaho", "cara"],
});
Console.WriteLine($"parallel {parallel.InstanceId} tasks={parallel.Tasks.Count}");

ReferResult? legal = null;
foreach (var task in parallel.Tasks)
{
    var advanced = await orch.CompleteAndAdvance(
        task.Id,
        task.AssigneeId,
        "تأیید شد",
        _ => new ReferInput
        {
            Title = "بررسی حقوقی",
            ToKind = AssigneeKind.Group,
            ToId = "legal",
        });
    Console.WriteLine(
        $"complete {task.AssigneeId} allCompleted={advanced.Complete.Completion.AllCompleted} next={(advanced.Next is null ? "—" : advanced.Next.InstanceId)}");
    if (advanced.Next is not null)
        legal = advanced.Next;
}

if (legal?.Task is null)
    throw new InvalidOperationException("expected auto-advance to legal after parallel join");

await eng.ClaimTask(legal.Task.Id, "mortenaho");
var last = await eng.CompleteTask(legal.Task.Id, "mortenaho", "ok");
Console.WriteLine($"legal allCompleted={last.Completion.AllCompleted}");
