using Microsoft.AspNetCore.Mvc;
using TheDecrypter.Application.Lookups;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Api.Controllers;

[ApiController]
[Route("api/cep")]
public class CepController(ICepRepository repo, ILookupService lookup) : ControllerBase
{
    /// <summary>Busca CEPs por padrão com curinga (ex.: ?pattern=88xxx500).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string pattern, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return BadRequest(new { message = "informe ?pattern= (ex.: 88xxx500)" });

        var hits = await repo.SearchWildcardAsync(pattern, Math.Clamp(limit, 1, 500), ct);
        return Ok(new { total = hits.Count, hits });
    }

    /// <summary>CEP exato (8 dígitos): base local de SC; se não achar, BrasilAPI.</summary>
    [HttpGet("{cep}")]
    public async Task<IActionResult> Get(string cep, CancellationToken ct)
    {
        try
        {
            var info = await lookup.CepAsync(cep, ct);
            return info is null ? NotFound(new { message = "CEP não encontrado." }) : Ok(info);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { message = "Provedor externo indisponível. Tente novamente." });
        }
    }
}
