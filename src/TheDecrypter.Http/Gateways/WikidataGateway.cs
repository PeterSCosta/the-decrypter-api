using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>
/// Filme por ID da IMDb, via SPARQL do Wikidata — sem chave e sem cota.
///
/// Esta classe é fina de propósito: monta a URL, faz o GET, entrega o corpo. A
/// consulta e a leitura da resposta vivem em <see cref="FilmeSparql"/>, no
/// Domain, porque é lá que estão as armadilhas — e é o Domain que o projeto de
/// teste alcança.
/// </summary>
public class WikidataGateway(HttpClient http) : IWikidataGateway
{
    public async Task<FilmeInfo?> FilmePorImdbAsync(string imdbId, CancellationToken ct = default) =>
        (await ResolverAsync(imdbId, ct)).Filme;

    public async Task<ResolucaoWikidata> ResolverAsync(string chave, CancellationToken ct = default)
    {
        var vazio = new ResolucaoWikidata(null, null);
        var sparql = FilmeSparql.Consulta(chave);
        if (sparql.Length == 0) return vazio;

        var url = "sparql?format=json&query=" + Uri.EscapeDataString(sparql);
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        // NÃO engolir: falha de rede ou do serviço tem de ser distinta de "não
        // achei". É essa distinção que o card inteiro depende para não dizer
        // "esse filme não existe" quando a verdade é "não consegui perguntar".
        resp.EnsureSuccessStatusCode();

        var corpo = await resp.Content.ReadAsStringAsync(ct);
        // Uma requisição, duas leituras: a consulta já traz os campos de filme e
        // os de item, e separá-las dobraria o custo de um endpoint público para
        // responder a mesma pergunta.
        return new ResolucaoWikidata(FilmeSparql.Ler(chave, corpo), FilmeSparql.LerItem(chave, corpo));
    }
}
