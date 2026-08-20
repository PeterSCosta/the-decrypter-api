using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TheDecrypter.Application.Lookups;
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
    IAirportRepository aeroportos,
    ICidRepository cids,
    ILoteBlumenauRepository lotes,
    ILookupService externos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] string q, CancellationToken ct)
    {
        var termo = (q ?? string.Empty).Trim();
        var quais = LookupShape.De(termo);
        // Vazio explícito, e não ausente: é a diferença entre "não sei procurar
        // isto" e "perguntei e não achei". Ver `LookupShape.Nomes`.
        if (quais == Consultas.Nenhuma) return Ok(new LookupResposta(termo));

        var digitos = new string([.. termo.Where(char.IsDigit)]);
        var r = new LookupResposta(termo) { Consultou = LookupShape.Nomes(quais) };

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

        if (quais.HasFlag(Consultas.CidCodigo))
            r.Cid = await cids.ByCodigoAsync(termo, ct);

        if (quais.HasFlag(Consultas.CidNome))
        {
            // Poucos: é sugestão ("você quis dizer"), não listagem. Quem quer a
            // tabela inteira tem a Biblioteca, que pagina.
            var achados = await cids.SearchAsync(termo, 5, ct);
            if (achados.Count > 0) r.Cids = achados;
        }

        if (quais.HasFlag(Consultas.LoteBlumenau))
        {
            // Quatro, e não um: sem os hífens o mesmo número admite mais de um
            // agrupamento real (2,3% deles, nunca mais que três). Quem escolhe
            // é quem digitou, olhando o endereço — não o servidor, no escuro.
            var achados = await lotes.BuscarAsync(termo, 4, ct);
            if (achados.Count == 1) r.Lote = achados[0];
            else if (achados.Count > 1) r.Lotes = achados;
        }

        if (quais.HasFlag(Consultas.Filme))
        {
            // A ÚNICA consulta daqui que sai para fora da nossa infraestrutura.
            // Se o Wikidata cair ou demorar, a exceção sobe e o cliente mostra
            // "não consegui perguntar" — que é diferente de "não encontrei", e
            // é a distinção que o card inteiro depende.
            // Uma requisição, duas leituras. `Q4941` é filme E item; `Q2` é só
            // item; `tt1074638` é só filme. Quem decide o que mostrar é a tela.
            var wd = await externos.WikidataAsync(termo, ct);
            r.Filme = wd.Filme;
            r.Item = wd.Item;
        }

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

    /// <summary>
    /// Quais consultas a forma da entrada abriu. **Vazio significa "não abri
    /// nenhuma"**, e é isso que separa "não achei" de "não sei procurar isto".
    ///
    /// Sem este campo os dois casos saem com a mesma cara — todas as chaves em
    /// `null` — e o cliente não tem como ser honesto. Ver `LookupShape.Nomes`.
    /// </summary>
    public IReadOnlyList<string> Consultou { get; set; } = [];
    public object? Cep { get; set; }
    public List<object>? CepsPrefixo { get; set; }
    public object? Municipio { get; set; }
    public object? Poste { get; set; }
    public object? Postes { get; set; }
    public object? CepCuringa { get; set; }
    public object? Aeroporto { get; set; }
    public object? Cid { get; set; }
    public object? Cids { get; set; }
    public object? Lote { get; set; }
    public object? Lotes { get; set; }
    public object? Filme { get; set; }

    /// <summary>
    /// O item do Wikidata quando a entrada é um `Q…` — rótulo, o que a coisa é,
    /// e a coordenada quando ela tem uma. Vem junto do `Filme` quando o item
    /// também é um filme.
    /// </summary>
    public object? Item { get; set; }
}
