namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// A ficha de um filme, com o TÍTULO BRASILEIRO separado de todos os outros.
///
/// ── POR QUE QUATRO CAMPOS DE TÍTULO, E NÃO UM ───────────────────────────────
/// Porque eles não são a mesma coisa, e trocá-los é a pior resposta possível
/// numa gincana: a prova cita o filme pelo nome que ele tem no Brasil.
///
///   <c>TituloBr</c>       "Um Sonho de Liberdade"  — o daqui
///   <c>TituloPt</c>       "Os Condenados de Shawshank" — PORTUGAL, não Brasil
///   <c>TituloOriginal</c> "The Shawshank Redemption"
///   <c>TituloIngles</c>   idem, quando o original não é em inglês
///
/// Cair de <c>TituloBr</c> para <c>TituloPt</c> quando o primeiro falta seria
/// devolver um nome plausível, em português, e ERRADO — "Regresso ao Futuro"
/// no lugar de "De Volta Para o Futuro". Por isso são campos distintos: quem
/// monta o card decide o que mostrar, sabendo o que cada um é.
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
}
