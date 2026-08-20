using System.Text.Json;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// As outras duas espécies de código do Wikidata — PROPRIEDADE e LEXEMA.
///
/// ── POR QUE ELAS PRECISAM DE CONSULTA PRÓPRIA ─────────────────────────────
/// Um item (`Q…`) tem rótulo, descrição e classificação. Uma propriedade
/// (`P…`) tem rótulo e descrição, mas não é uma coisa — é o CAMPO: `P345` é
/// "identificador IMDb", e a descrição dele explica os prefixos `tt`, `nm` e
/// `co`. Um lexema (`L…`) não tem rótulo nenhum: tem **lema**, língua, classe
/// gramatical e sentidos, num vocabulário RDF completamente diferente
/// (`wikibase:lemma`, `dct:language`, `ontolex:sense`).
///
/// Forçar as três na mesma consulta produziria um `OPTIONAL` gigante em que
/// dois terços nunca casam — mais lento e mais difícil de ler do que três
/// consultas que dizem o que procuram.
///
/// ── E POR QUE AS TRÊS PODEM, ENQUANTO A BUSCA POR NOME NÃO ────────────────
/// Todas são acerto EXATO: um código aponta para um registro e só um. A recusa
/// da Onda 10 era sobre ambiguidade de NOME — "Maria" devolve 113 candidatos.
/// Aqui não há candidato: há o registro, ou nada.
/// </summary>
public static class WikidataSparql
{
    private const string ModeloPropriedade = """
        SELECT ?rotPt ?rotEn ?descPt ?descEn WHERE {
          OPTIONAL { wd:@ID rdfs:label ?rotPt . FILTER(LANG(?rotPt) = "pt") }
          OPTIONAL { wd:@ID rdfs:label ?rotEn . FILTER(LANG(?rotEn) = "en") }
          OPTIONAL { wd:@ID schema:description ?descPt . FILTER(LANG(?descPt) = "pt") }
          OPTIONAL { wd:@ID schema:description ?descEn . FILTER(LANG(?descEn) = "en") }
        }
        LIMIT 1
        """;

    private const string ModeloLexema = """
        SELECT ?lema ?linguaL ?catL (GROUP_CONCAT(DISTINCT ?glosa; separator=" · ") AS ?glosas)
        WHERE {
          wd:@ID wikibase:lemma ?lema ; dct:language ?lingua ; wikibase:lexicalCategory ?cat .
          OPTIONAL { ?lingua rdfs:label ?linguaL . FILTER(LANG(?linguaL) = "pt") }
          OPTIONAL { ?cat rdfs:label ?catL . FILTER(LANG(?catL) = "pt") }
          OPTIONAL { wd:@ID ontolex:sense ?s . ?s skos:definition ?glosa . FILTER(LANG(?glosa) IN ("pt", "en")) }
        }
        GROUP BY ?lema ?linguaL ?catL
        LIMIT 1
        """;

    /// <summary>
    /// A consulta para `P…` ou `L…`. Vazio para qualquer outra coisa —
    /// inclusive `Q…`, que tem consulta própria em <see cref="FilmeSparql"/>
    /// porque ela também precisa responder "é filme?".
    /// </summary>
    public static string Consulta(string? codigo)
    {
        var id = CodigoWikidata.Normalizar(codigo);
        return CodigoWikidata.Especie(codigo) switch
        {
            EspecieWikidata.Propriedade => ModeloPropriedade.Replace("@ID", id),
            EspecieWikidata.Lexema => ModeloLexema.Replace("@ID", id),
            _ => string.Empty,
        };
    }

    /// <summary>O corpo da resposta → item. `null` quando o código não existe.</summary>
    public static ItemWikidata? Ler(string codigo, string json)
    {
        var id = CodigoWikidata.Normalizar(codigo);
        var especie = CodigoWikidata.Especie(codigo);
        if (id.Length == 0 || string.IsNullOrWhiteSpace(json)) return null;
        if (especie is not (EspecieWikidata.Propriedade or EspecieWikidata.Lexema)) return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("results", out var res)
                || !res.TryGetProperty("bindings", out var b)
                || b.ValueKind != JsonValueKind.Array || b.GetArrayLength() == 0)
                return null;

            var r = b[0];
            return especie == EspecieWikidata.Propriedade
                ? LerPropriedade(id, r)
                : LerLexema(id, r);
        }
    }

    private static ItemWikidata? LerPropriedade(string id, JsonElement r)
    {
        var rotulo = V(r, "rotPt") ?? V(r, "rotEn");
        var descricao = V(r, "descPt") ?? V(r, "descEn");
        // Sem rótulo nem descrição não há propriedade: é um número que o
        // Wikidata não conhece, e uma casca com o número dentro seria afirmar
        // existência sem evidência.
        if (rotulo is null && descricao is null) return null;

        return new ItemWikidata(
            id, rotulo,
            V(r, "rotPt") is not null ? "pt" : "en",
            descricao,
            // O que ela É: um campo, não uma coisa. Dizer isso evita que alguém
            // leia "identificador IMDb" como se fosse um filme chamado assim.
            ["propriedade do Wikidata"],
            null, null, null, false);
    }

    private static ItemWikidata? LerLexema(string id, JsonElement r)
    {
        var lema = V(r, "lema");
        if (lema is null) return null;

        // A língua e a classe gramatical são o que a palavra É — e a língua
        // importa: `L1` é "ama", mas em SUMÉRIO, e ler isso como português
        // seria a resposta errada com melhor disfarce que este card poderia
        // dar numa bancada em que o vocabulário é pt-BR.
        var tipos = new List<string> { "lexema" };
        if (V(r, "catL") is { } cat) tipos.Add(cat);
        if (V(r, "linguaL") is { } lingua) tipos.Add(lingua);

        return new ItemWikidata(
            id, lema, null,
            V(r, "glosas"),
            [.. tipos],
            null, null, null, false);
    }

    private static string? V(JsonElement e, string chave) =>
        e.TryGetProperty(chave, out var n) && n.TryGetProperty("value", out var v)
            && v.GetString() is { Length: > 0 } s && !string.IsNullOrWhiteSpace(s)
            ? s.Trim()
            : null;
}
