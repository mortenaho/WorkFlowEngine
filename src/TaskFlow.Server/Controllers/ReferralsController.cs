using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application;

namespace TaskFlow.Server.Controllers;

[Route("v1/referrals")]
public sealed class ReferralsController(Engine engine) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReferralRequest? body, CancellationToken cancellationToken)
    {
        body ??= new();
        var from = body.From.Length > 0 ? body.From : Request.Actor();
        var result = await engine.Refer(from, new ReferInput
        {
            DefinitionKey = body.DefinitionKey,
            ParentInstanceId = body.ParentInstanceId,
            Title = body.Title,
            Parameters = body.Parameters,
            ToKind = body.To.Kind,
            ToId = body.To.Id,
            ToIds = body.To.Ids,
        }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result.ToDto());
    }
}
