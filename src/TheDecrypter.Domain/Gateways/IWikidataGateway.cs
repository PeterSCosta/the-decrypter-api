namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// A ficha de um filme, com o TÍTULO BRASILEIRO separado de todos os outros.
///
/// ── POR QUE QUATRO CAMPOS DE TÍTULO, E NÃO UM ───────────────────────────────
/// Porque eles não são a mesma coisa, e trocá-los é a pior resposta possível
/// numa gincana: a prova cita o filme pelo nome que ele tem no Brasil.
///
///   <c>TituloBr</c>       "Um Sonho de Liberdade"  — marcado `pt-br`, é o daqui
///   <c>TituloPt</c>       "Os Condenados de Shawshank" — marcado só `pt`
///   <c>TituloOriginal</c> "The Shawshank Redemption"
///   <c>TituloIngles</c>   idem, quando o original não é em inglês
///
/// ── E A ETIQUETA `pt` NÃO DIZ O PAÍS ───────────────────────────────────────
/// `pt` no Wikidata significa PORTUGUÊS, não Portugal — e os dois usos
/// convivem na mesma etiqueta. "Regresso ao Futuro" é de Portugal; "007 -
/// Operação Skyfall", marcado igual, é o título BRASILEIRO. Não há como
/// distinguir pelo dado.
///
/// Daí a regra: `pt` nunca entra na escada do título principal, porque cair
/// nele quando falta o `pt-br` acertaria metade das vezes e erraria a outra
/// metade — resposta errada com confiança. Ele aparece na ficha, rotulado pelo
/// que de fato é: "em português, sem marca de país".
///
/// ── A COBERTURA, MEDIDA (2026-08-20) ────────────────────────────────────────
/// Sobre os filmes de 2019 com ID da IMDb no Wikidata, quantos têm título pt-BR
/// (rótulo OU apelido): <b>6,2%</b> no geral · 11,7% com ≥10 wikis · 35,6% com
/// ≥25 · <b>66,7% com ≥50</b>. Ou seja: um terço dos filmes MAIS conhecidos de
/// 2019 não tem título brasileiro aqui. <c>TituloBr</c> nulo é o caso comum, e
/// a tela precisa dizer isso — não preencher com o que sobrou.
/// </summary>
public record FilmeInfo(
    string ImdbId,
    string? TituloBr,
    string? TituloPt,
    string? TituloOriginal,
    string? TituloIngles,
    int? Ano,
    int? DuracaoMin,
    string[]? Direcao,
    string[]? Generos,
    string[]? Paises,
    string? WikidataId,
    string Fonte);

/// <summary>
/// Um item QUALQUER do Wikidata — o que sobra quando ele não é filme.
///
/// ── POR QUE ISTO EXISTE, E POR QUE NÃO CONTRADIZ A ONDA 10 ────────────────
/// A avaliação da Onda 10 recusou resolver NOME → entidade, e a razão era a
/// ambiguidade: "Bacurau" é filme e é ave, "Maria" são 113 candidatos. Um QID
/// não tem esse problema — ele identifica **um** item e só um, por construção.
/// É acerto exato, não triagem.
///
/// E o que ele devolve é justamente o que a bancada sabe usar: um rótulo, uma
/// frase dizendo o que a coisa é, e — quando existe — uma COORDENADA, que é o
/// domínio central desta casa. `Q155` é o Brasil e vem com ponto no mapa.
///
/// ── O QUE ELE NÃO FAZ ─────────────────────────────────────────────────────
/// Não vira busca por nome, não vira ficha de pessoa, não vira catálogo. É a
/// resposta honesta a "o que é este código", e para. Quem quiser mais tem o
/// link do próprio Wikidata no card.
/// </summary>
public record ItemWikidata(
    string Qid,
    /// <summary>O rótulo, na melhor língua disponível — inclusive `mul`.</summary>
    string? Rotulo,
    /// <summary>A língua do rótulo, para a tela poder dizer de onde ele veio.</summary>
    string? Lingua,
    /// <summary>A frase de uma linha que o Wikidata usa para desambiguar.</summary>
    string? Descricao,
    /// <summary>O que a coisa É (P31), em pt.</summary>
    string[]? Tipos,
    /// <summary>Identificador na IMDb, quando o item tem um — `tt…` ou `nm…`.</summary>
    string? ImdbId,
    double? Lat,
    double? Lng,
    bool EhFilme);

/// <summary>
/// Consulta de obra por identificador externo, no Wikidata.
///
/// Sem chave, sem cota e sem cadastro — é o que torna esta porta viável. O
/// custo dela não é dinheiro: é cobertura (ver <see cref="FilmeInfo"/>).
/// </summary>
public interface IWikidataGateway
{
    /// <summary>
    /// <c>tt0111161</c> → ficha, ou <c>null</c> quando o Wikidata não conhece o ID.
    /// </summary>
    /// <remarks>
    /// <b>null significa "não achei no Wikidata", NUNCA "o filme não existe".</b>
    /// São coisas diferentes e a tela precisa dizer a certa: o Wikidata cobre
    /// uma fração do catálogo da IMDb. Falha de rede lança, para o chamador
    /// distinguir "não consegui perguntar" de "perguntei e não tem".
    /// </remarks>
    Task<FilmeInfo?> FilmePorImdbAsync(string imdbId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a chave UMA vez e devolve as duas leituras.
    ///
    /// Uma requisição, não duas: a consulta já traz os campos de filme e os de
    /// item, e separá-las em duas chamadas dobraria o custo de um endpoint
    /// público para responder a mesma pergunta.
    /// </summary>
    Task<ResolucaoWikidata> ResolverAsync(string chave, CancellationToken ct = default);
}

/// <summary>As duas leituras da mesma resposta. Ambas podem vir nulas.</summary>
public record ResolucaoWikidata(FilmeInfo? Filme, ItemWikidata? Item);
