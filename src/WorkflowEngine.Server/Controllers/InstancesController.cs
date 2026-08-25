using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Application;

namespace WorkflowEngine.Server.Controllers;

[Route("v1/instances")]
public sealed class InstancesController(Engine engine) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var inst = await engine.GetInstance(id, cancellationToken);
        return Ok(inst.ToDto());
    }

    [HttpGet("{id}/tasks")]
    public async Task<IActionResult> Tasks(string id, CancellationToken cancellationToken)
    {
        var tasks = await engine.ListTasksByInstance(id, cancellationToken);
        return Ok(tasks.Select(t => t.ToDto()));
    }

    [HttpGet("{id}/completion")]
    public async Task<IActionResult> Completion(string id, CancellationToken cancellationToken)
    {
        var comp = await engine.Completion(id, cancellationToken);
        return Ok(comp.ToDto());
    }
}
