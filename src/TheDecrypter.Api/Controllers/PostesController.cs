using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Api.Controllers;

/// <summary>
/// Postes de iluminação pública de Blumenau (45.285 pontos).
///
/// Quatro formas de perguntar, porque são quatro perguntas diferentes: "que
/// poste é a plaqueta X", "o que tem perto daqui", "quais são os desta rua" e
/// "o que aparece nesta parte do mapa".
/// </summary>
[ApiController]
[Route("api/postes")]
[Authorize]
[OutputCache(PolicyName = "lookups")]
public class PostesController(IPosteRepository repo) : ControllerBase
{
    /// <summary>Plaqueta exata. "0338" e "338" são postes diferentes.</summary>
    [HttpGet("{plaqueta}")]
    public async Task<IActionResult> PorPlaqueta(string plaqueta, CancellationToken ct)
    {
        var p = await repo.ByPlaquetaAsync(plaqueta.Trim(), ct);
        return p is null ? NotFound(new { message = "plaqueta não encontrada" }) : Ok(p);
    }

    /// <summary>?q= plaqueta (prefixo) ou nome de rua/bairro.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Buscar(
        [FromQuery] string q, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "informe ?q= (plaqueta, rua ou bairro)" });
        var hits = await repo.SearchAsync(q, limit, ct);
        return Ok(new { total = hits.Count, hits });
    }

    /// <summary>Os mais próximos de um ponto, com a distância em metros.</summary>
    [HttpGet("near")]
    public async Task<IActionResult> Proximos(
        [FromQuery] double lat, [FromQuery] double lng,
        [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            return BadRequest(new { message = "coordenada fora de faixa" });
        var hits = await repo.NearAsync(lat, lng, limit, ct);
        return Ok(new { total = hits.Count, hits });
    }

    /// <summary>Dentro da caixa do mapa. `truncado` avisa que o teto foi atingido.</summary>
    [HttpGet("bbox")]
    public async Task<IActionResult> Caixa(
        [FromQuery] double sul, [FromQuery] double norte,
        [FromQuery] double oeste, [FromQuery] double leste,
        [FromQuery] int limit = 2000, CancellationToken ct = default)
    {
        if (norte <= sul || leste <= oeste)
            return BadRequest(new { message = "caixa inválida (norte>sul e leste>oeste)" });
        var (hits, truncado) = await repo.BboxAsync(sul, norte, oeste, leste, limit, ct);
        return Ok(new
        {
            total = hits.Count,
            truncado,
            // A caixa no zoom da cidade contém as 45 mil linhas; o app usa isto
            // para dizer "aproxime para ver todos" em vez de mentir que acabou.
            message = truncado ? "Aproxime o mapa para ver todos os postes desta área." : null,
            hits,
        });
    }
}
