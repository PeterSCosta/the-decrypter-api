using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Api.Controllers;

[ApiController]
[Route("api/municipio")]
[Authorize]
[OutputCache(PolicyName = "lookups")]
public class MunicipioController(IMunicipioRepository repo) : ControllerBase
{
    /// <summary>Busca município por nome (?q=).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "informe ?q=" });

        var hits = await repo.SearchByNameAsync(q, limit, ct);
        return Ok(new { total = hits.Count, hits });
    }

    /// <summary>Município pelo código IBGE (7 ou 6 dígitos).</summary>
    [HttpGet("{code}")]
    public async Task<IActionResult> Get(string code, CancellationToken ct)
    {
        var m = await repo.GetByCodeAsync(code, ct);
        return m is null ? NotFound(new { message = "município não encontrado" }) : Ok(m);
    }
}
