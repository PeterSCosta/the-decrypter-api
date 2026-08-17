using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Api.Controllers;

/// <summary>
/// Identificar a música de um trecho de áudio.
///
/// SEM OutputCache, ao contrário dos outros controllers: a resposta depende do
/// CORPO enviado, e cachear por URL devolveria a música de outro trecho.
/// </summary>
[ApiController]
[Route("api/musica")]
[Authorize]
public class MusicaController(IMusicGateway gateway, ILogger<MusicaController> log) : ControllerBase
{
    /// <summary>
    /// Teto de payload.
    ///
    /// 12 segundos de WAV mono a 16 kHz dão ~384 KB; 4 MB cobre folgado quem
    /// mandar estéreo ou um trecho maior, e ainda barra o upload de um arquivo
    /// inteiro — que gastaria cota para responder pior, já que o serviço
    /// identifica melhor um trecho limpo do que uma faixa com várias músicas.
    /// </summary>
    private const long TetoBytes = 4 * 1024 * 1024;

    [HttpPost("identificar")]
    [RequestSizeLimit(TetoBytes)]
    public async Task<IActionResult> Identificar(IFormFile? arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { message = "Mande um trecho de áudio no campo `arquivo`." });

        if (arquivo.Length > TetoBytes)
            return BadRequest(new { message = "Trecho grande demais. Recorte um pedaço menor (10 a 15 segundos bastam)." });

        // As duas respostas negativas são DIFERENTES e levam a ações opostas:
        // "não reconheci" convida a recortar outro trecho; "sem chave" é perda
        // de tempo até alguém mexer no servidor. Devolver o mesmo JSON para as
        // duas faria a pessoa tentar dez vezes à toa.
        if (!gateway.Configurado)
        {
            return Ok(new
            {
                reconhecido = false,
                configurado = false,
                message = "O reconhecimento de música não está configurado neste servidor.",
            });
        }

        await using var stream = arquivo.OpenReadStream();
        var achado = await gateway.IdentificarAsync(stream, arquivo.FileName, ct);

        if (achado is null)
        {
            log.LogInformation("Trecho não reconhecido ({Bytes} bytes)", arquivo.Length);
            return Ok(new { reconhecido = false, configurado = true });
        }

        return Ok(new { reconhecido = true, configurado = true, musica = achado });
    }
}
