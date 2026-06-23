using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Api.Controllers;

[ApiController]
[Route("api/fleet")]
[OutputCache(PolicyName = "fleet")]
public class FleetController(IFleetGateway gateway) : ControllerBase
{
    /// <summary>Frota (Traccar) com última posição. Vazio se o Traccar não estiver configurado.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await gateway.GetFleetAsync(ct));
}
