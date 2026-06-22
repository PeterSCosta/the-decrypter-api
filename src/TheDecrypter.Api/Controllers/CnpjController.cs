using Microsoft.AspNetCore.Mvc;
using TheDecrypter.Application.Cnpj;

namespace TheDecrypter.Api.Controllers;

[ApiController]
[Route("api/cnpj")]
public class CnpjController(ICnpjService service) : ControllerBase
{
    /// <summary>CNPJ → empresa (cache-first; provedor externo rate-limited via Polly).</summary>
    [HttpGet("{cnpj}")]
    public async Task<IActionResult> Get(string cnpj, CancellationToken ct)
    {
        try
        {
            var info = await service.LookupAsync(cnpj, ct);
            return info is null ? NotFound(new { message = "CNPJ não encontrado." }) : Ok(info);
        }
        catch (HttpRequestException)
        {
            // upstream indisponível/limitado após retries: resposta limpa (não 500)
            return StatusCode(503, new { message = "Provedor externo indisponível. Tente novamente." });
        }
    }
}
