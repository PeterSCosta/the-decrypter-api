using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TheDecrypter.Application.Lookups;

namespace TheDecrypter.Api.Controllers;

[ApiController]
[Route("api")]
[OutputCache(PolicyName = "lookups")]
public class LookupsController(ILookupService svc) : ControllerBase
{
    private async Task<IActionResult> Wrap<T>(Func<Task<T?>> load) where T : class
    {
        try
        {
            var r = await load();
            return r is null ? NotFound(new { message = "não encontrado" }) : Ok(r);
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { message = "Provedor externo indisponível. Tente novamente." });
        }
    }

    [HttpGet("isbn/{isbn}")]
    public Task<IActionResult> Isbn(string isbn, CancellationToken ct) => Wrap(() => svc.IsbnAsync(isbn, ct));

    [HttpGet("ncm/{code}")]
    public Task<IActionResult> Ncm(string code, CancellationToken ct) => Wrap(() => svc.NcmAsync(code, ct));

    [HttpGet("registrobr/{domain}")]
    public Task<IActionResult> RegistroBr(string domain, CancellationToken ct) =>
        Wrap(() => svc.RegistroBrAsync(domain, ct));

    [HttpGet("produto/{barcode}")]
    public Task<IActionResult> Produto(string barcode, CancellationToken ct) =>
        Wrap(() => svc.ProductAsync(barcode, ct));

    [HttpGet("pix/{ispb}")]
    public Task<IActionResult> Pix(string ispb, CancellationToken ct) => Wrap(() => svc.PixAsync(ispb, ct));

    /// <summary>Lista completa de participantes PIX (front indexa client-side).</summary>
    [HttpGet("pix")]
    public async Task<IActionResult> PixList(CancellationToken ct) => Ok(await svc.PixListAsync(ct));

    [HttpGet("what3words/{words}")]
    public Task<IActionResult> What3Words(string words, CancellationToken ct) =>
        Wrap(() => svc.W3wAsync(words, ct));

    [HttpGet("geocode")]
    public Task<IActionResult> Geocode([FromQuery] string q, CancellationToken ct) =>
        Wrap(() => svc.GeocodeAsync(q ?? string.Empty, ct));
}
