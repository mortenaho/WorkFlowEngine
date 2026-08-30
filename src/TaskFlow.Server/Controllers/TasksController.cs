using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application;

namespace TaskFlow.Server.Controllers;

[Route("v1/tasks")]
public sealed class TasksController(Engine engine) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Pending([FromQuery] string? user, [FromQuery] string? group, CancellationToken cancellationToken)
    {
        user ??= "";
        group ??= "";
        if (user.Length == 0 && group.Length == 0)
            user = Request.Actor();
        var tasks = await engine.PendingTasks(user, group, cancellationToken);
        return Ok(tasks.Select(t => t.ToDto()));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var task = await engine.GetTask(id, cancellationToken);
        return Ok(task.ToDto());
    }

    [HttpPost("{id}/claim")]
    public async Task<IActionResult> Claim(string id, CancellationToken cancellationToken)
    {
        var task = await engine.ClaimTask(id, await Request.DecodeActor(), cancellationToken);
        return Ok(task.ToDto());
    }

    [HttpPost("{id}/unclaim")]
    public async Task<IActionResult> Unclaim(string id, CancellationToken cancellationToken)
    {
        var task = await engine.UnclaimTask(id, await Request.DecodeActor(), cancellationToken);
        return Ok(task.ToDto());
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(string id, CancellationToken cancellationToken)
    {
        var body = await Request.ReadBodyOrEmpty<CompleteRequest>();
        var who = body.From.Length > 0 ? body.From : Request.Actor();
        var result = await engine.CompleteTask(id, who, body.Note, body.Parameters, cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPost("{id}/complete-and-end")]
    public async Task<IActionResult> CompleteAndEnd(string id, CancellationToken cancellationToken)
    {
        var body = await Request.ReadBodyOrEmpty<CompleteRequest>();
        var who = body.From.Length > 0 ? body.From : Request.Actor();
        var result = await engine.CompleteAndEnd(id, who, body.Note, body.Parameters, cancellationToken);
        return Ok(result.ToDto());
    }
}
