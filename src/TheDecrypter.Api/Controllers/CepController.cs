using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using TheDecrypter.Application.Lookups;
using TheDecrypter.Domain.Export;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Api.Controllers;

[ApiController]
[Route("api/cep")]
[Authorize]
[OutputCache(PolicyName = "lookups")]
public class CepController(ICepRepository repo, ILookupService lookup) : ControllerBase
{
    /// <summary>Busca CEPs por padrão com curinga (ex.: ?pattern=88xxx500).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string pattern, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return BadRequest(new { message = "informe ?pattern= (ex.: 88xxx500)" });

        var (hits, total) = await repo.SearchWildcardAsync(pattern, Math.Clamp(limit, 1, 500), ct);
        // `total` é a contagem real; `hits` vem limitado. O app mostra o total
        // no rótulo ("88xxx500 · 213 CEP(s)") e a lista truncada no card.
        return Ok(new { total, truncado = hits.Count < total, hits });
    }

    /// <summary>
    /// Teto do arquivo. A base de SC inteira são 40.445 CEPs, e o pior padrão
    /// que passa no portão (`8xxxxxxx`) casa quase ela toda — então hoje isto
    /// nunca dispara. Existe para o dia do CEP nacional (~1,2 M linhas), que é
    /// o dia em que ninguém vai lembrar de pôr um teto.
    /// </summary>
    private const int LimiteExportacao = 100_000;

    /// <summary>
    /// A lista COMPLETA de um padrão com curinga, em CSV.
    ///
    /// O card da bancada mostra 12 linhas de propósito — ninguém lê 40 mil num
    /// card. Este endpoint é a outra metade: o arquivo traz todos.
    ///
    /// A ROTA NÃO TERMINA EM `.csv`, e isso não é gosto. A API fica atrás da
    /// Cloudflare (ver `ClientIp.CabecalhoCloudflare`), que guarda resposta no
    /// edge POR EXTENSÃO de arquivo e monta a chave com URL+query — sem o
    /// `Authorization`. Um `/cep/search.csv?pattern=88xxx500` cacheado seria a
    /// base de CEP servida a quem não mandou token nenhum, com o `[Authorize]`
    /// intacto e contornado. Sem extensão a heurística não liga; os cabeçalhos
    /// abaixo são o cinto.
    /// </summary>
    [HttpGet("export")]
    [OutputCache(NoStore = true)]
    [EnableRateLimiting("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string pattern, CancellationToken ct = default)
    {
        // O MESMO portão do lookup automático, e não um parecido: `xxxxxxxx`
        // traduz para um regex válido (`^\d{8}$`) e casaria a base inteira.
        // `LookupShape` já recusa isso com o motivo escrito lá.
        //
        // O `/cep/search` acima continua devolvendo 200 com lista vazia para
        // padrão inválido. É contraditório e eu sei: ele tem consumidor fora
        // deste repositório (o painel da equipe roda sobre esta API), e mudar
        // o contrato dele não é assunto de um botão de download.
        if (string.IsNullOrWhiteSpace(pattern)
            || !LookupShape.ParecePadraoDeCep(pattern)
            || CepPattern.Traduzir(pattern) is null)
        {
            return BadRequest(new
            {
                message = "Padrão inválido — use dígitos e curingas, com ao menos um dígito (ex.: 88xxx500).",
            });
        }

        // Guarda-chuva pós-materialização: o repositório já trouxe as linhas
        // quando o teto é conferido. Aceitável enquanto a base for só SC (o
        // teto é 2,5× a base inteira, inalcançável); no dia do CEP nacional
        // esta linha vira um `ContarCuringaAsync` antes do `Search`.
        var (hits, total) = await repo.SearchWildcardAsync(pattern, LimiteExportacao, ct);
        if (total == 0) return NotFound(new { message = "Nenhum CEP casa esse padrão." });
        if (total > LimiteExportacao)
            return BadRequest(new { message = $"São {total} CEPs — mais do que o arquivo aceita. Refine o padrão." });

        // `OutputCache(NoStore)` é do NOSSO cache, em memória; não escreve
        // cabeçalho nenhum e a borda não fica sabendo de nada. Quem fala com a
        // Cloudflare é isto:
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Vary = "Authorization";

        return File(CepCsv.Gerar(hits), "text/csv; charset=utf-8", CepCsv.NomeDoArquivo(pattern));
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
