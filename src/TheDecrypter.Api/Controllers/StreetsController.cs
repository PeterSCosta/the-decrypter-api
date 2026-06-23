using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Api.Controllers;

[ApiController]
[Route("api/streets")]
[OutputCache(PolicyName = "lookups")]
public class StreetsController(IStreetRepository repo) : ControllerBase
{
    /// <summary>Rol de Ruas (Blumenau): ?q= código, Nº da Lei, data (dd/mm/aaaa) ou nome.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "informe ?q= (código, lei, data ou nome)" });

        var hits = await repo.SearchAsync(q, limit, ct);
        return Ok(new { total = hits.Count, hits });
    }
}
