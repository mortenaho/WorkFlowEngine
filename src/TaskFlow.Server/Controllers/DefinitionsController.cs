using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application;

namespace TaskFlow.Server.Controllers;

[Route("v1/definitions")]
public sealed class DefinitionsController(Engine engine) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDefinitionRequest? body, CancellationToken cancellationToken)
    {
        body ??= new();
        var def = await engine.Register(body.Key, body.Name, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, def.ToDto());
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
    {
        var def = await engine.GetDefinitionByKey(key, cancellationToken);
        return Ok(def.ToDto());
    }
}
