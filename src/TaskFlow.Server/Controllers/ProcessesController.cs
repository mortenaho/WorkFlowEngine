using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application;

namespace TaskFlow.Server.Controllers;

[Route("v1/processes")]
public sealed class ProcessesController(Engine engine) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartProcessRequest? body, CancellationToken cancellationToken)
    {
        body ??= new();
        if (body.Initiator.Length == 0)
            body.Initiator = Request.Actor();
        var result = await engine.Start(body.ProcessKey, body.Initiator, body.Parameters, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.ToDto());
    }

    [HttpGet("{processKey}/instances")]
    public async Task<IActionResult> ListInstances(string processKey, CancellationToken cancellationToken)
    {
        var list = await engine.ListByProcessKey(processKey, cancellationToken);
        return Ok(list.ToDto());
    }
}
