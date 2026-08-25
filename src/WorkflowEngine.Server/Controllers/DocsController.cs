using Microsoft.AspNetCore.Mvc;

namespace WorkflowEngine.Server.Controllers;

public sealed class DocsController : ControllerBase
{
    [HttpGet("/")]
    [HttpGet("/docs")]
    public IActionResult Home() => Redirect("/swagger");

    [HttpGet("/openapi.yaml")]
    public IActionResult OpenApi() => Embedded("openapi.yaml", "application/yaml; charset=utf-8");

    [HttpGet("/swagger")]
    public IActionResult Swagger() => Embedded("swagger.html", "text/html; charset=utf-8");

    private IActionResult Embedded(string fileName, string contentType)
    {
        var asm = typeof(DocsController).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return NotFound();
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return Content(reader.ReadToEnd(), contentType);
    }
}
