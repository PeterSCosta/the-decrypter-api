using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Api.Controllers;

/// <summary>
/// Uma pergunta, uma resposta: o que a entrada da bancada pode ser.
///
/// POR QUE MULTIPLEXADO, e não quatro endpoints em paralelo: o fan-out do app é
/// **síncrono** e roda os ~100 decoders a cada mudança do contexto. Quatro
/// respostas chegando em momentos diferentes = quatro fan-outs e quatro
/// repaints por tecla, com a lista de resultados reordenando na frente de quem
/// digita. Uma resposta só = um fan-out. É decisão de renderização, não de rede
/// (em HTTP/2 quatro GETs custam praticamente um round-trip).
///
/// De quebra, o teto de requisições por IP é dividido por uma equipe inteira
/// atrás do NAT do local — quatro vezes menos consultas importa ali.
/// </summary>
[ApiController]
[Route("api/lookup")]
[Authorize]
[OutputCache(PolicyName = "lookups")]
public class LookupController(
    ICepRepository ceps,
    IMunicipioRepository municipios,
    IPosteRepository postes,
    IAirportRepository aeroportos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] string q, CancellationToken ct)
    {
        var termo = (q ?? string.Empty).Trim();
        var quais = LookupShape.De(termo);
        if (quais == Consultas.Nenhuma) return Ok(new LookupResposta(termo));

        var digitos = new string([.. termo.Where(char.IsDigit)]);
        var r = new LookupResposta(termo);

        // SEQUENCIAL, e não `Task.WhenAll`: as sub-consultas dividem o mesmo
        // `DbContext` com escopo de requisição, e duas operações simultâneas
        // nele lançam InvalidOperationException. São consultas indexadas
        // sub-milissegundo; paralelizar traria bug, não velocidade.
        if (quais.HasFlag(Consultas.CepExato))
        {
            // Direto no repositório: `LookupService.CepAsync` cai para a
            // BrasilAPI quando não acha, e aqui isso significaria bater num
            // provedor externo a cada tecla de um CEP que não existe.
            r.Cep = await ceps.GetByCodeAsync(digitos, ct);
        }

        if (quais.HasFlag(Consultas.CepPrefixoSc))
        {
            // 6 dígitos: pode ser um CEP de SC sem o prefixo 88 ou 89.
            foreach (var prefixo in (string[])["88", "89"])
            {
                if (await ceps.GetByCodeAsync(prefixo + digitos, ct) is { } achado)
                    (r.CepsPrefixo ??= []).Add(achado);
            }
        }

        if (quais.HasFlag(Consultas.Municipio))
            r.Municipio = await municipios.GetByCodeAsync(digitos, ct);

        if (quais.HasFlag(Consultas.Plaqueta))
            r.Poste = await postes.ByPlaquetaAsync(termo, ct);

        if (quais.HasFlag(Consultas.RuaOuBairro))
            r.Postes = await postes.SearchAsync(termo, 20, ct);

        if (quais.HasFlag(Consultas.Aeroporto))
            r.Aeroporto = await aeroportos.ByCodeAsync(termo, ct);

        if (quais.HasFlag(Consultas.CepCuringa))
        {
            var (hits, total) = await ceps.SearchWildcardAsync(termo, 12, ct);
            if (total > 0) r.CepCuringa = new { total, hits };
        }

        return Ok(r);
    }
}

/// <summary>Só o que a forma da entrada pediu; o resto vem nulo.</summary>
public class LookupResposta(string q)
{
    public string Q { get; } = q;
    public object? Cep { get; set; }
    public List<object>? CepsPrefixo { get; set; }
    public object? Municipio { get; set; }
    public object? Poste { get; set; }
    public object? Postes { get; set; }
    public object? CepCuringa { get; set; }
    public object? Aeroporto { get; set; }
}
