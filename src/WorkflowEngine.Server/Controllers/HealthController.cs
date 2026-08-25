using Microsoft.AspNetCore.Mvc;

namespace WorkflowEngine.Server.Controllers;

[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
