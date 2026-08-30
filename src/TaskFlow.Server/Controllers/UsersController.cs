using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application;

namespace TaskFlow.Server.Controllers;

[Route("v1/users")]
public sealed class UsersController(Engine engine) : ControllerBase
{
    [HttpGet("{user}/processes")]
    public async Task<IActionResult> Processes(string user, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        if (user.Length == 0)
            user = Request.Actor();
        var list = await engine.ListUserProcesses(user, state ?? "", cancellationToken);
        return Ok(list.ToDto());
    }
}
