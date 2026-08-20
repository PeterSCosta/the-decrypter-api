using System.Globalization;
using System.Text;
using System.Text.Json;

using TheDecrypter.Domain.Search;

namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// A consulta SPARQL do filme e a LEITURA da resposta — puras, sem HttpClient.
///
/// Estão aqui, e não no gateway, porque é aqui que mora tudo o que pode dar
/// errado em silêncio; o gateway é só "monta URL, faz GET, entrega o corpo".
/// O projeto de teste alcança o Domain e não alcança o Http, então esta
/// separação é a diferença entre as três armadilhas abaixo terem teste e não
/// terem.
///
/// ── AS TRÊS ARMADILHAS, MEDIDAS CONTRA O ENDPOINT DE VERDADE ────────────────
///
/// <b>1. A duração vem com UNIDADE.</b> `Oppenheimer` (tt15398776) tem P2047 =
/// <c>10809</c> com unidade <c>Q11574</c> (segundo); quase todo o resto vem em
/// <c>Q7727</c> (minuto). Ler a quantidade sem a unidade imprime "10809 min"
/// para um filme de 180 — e a primeira versão desta consulta fez isso. Daí o
/// caminho <c>p:P2047/psv:P2047</c> em vez do atalho <c>wdt:</c>.
///
/// <b>2. A data de lançamento é UMA POR PAÍS.</b> Pegar qualquer P577 devolvia
/// 1999 para <i>Close-Up</i>, que é de 1990, e 1995 para <i>Um Sonho de
/// Liberdade</i>, que é de 1994. O ano do filme é o <c>MIN</c>.
///
/// <b>3. O título brasileiro é APELIDO, não rótulo.</b> A que mais importa.
/// `tt0111161` tem <c>rdfs:label</c>@pt-br = "The Shawshank Redemption" — o
/// inglês ocupando o campo do português. "Um Sonho de Liberdade" está em
/// <c>skos:altLabel</c>@pt-br. Uma consulta que lesse só o rótulo concluiria
/// que o Wikidata não tem o título daqui, quando tem.
///
/// E os apelidos vêm em LISTA, não um: <c>SAMPLE()</c> escolhe um qualquer, e
/// numa medição ele devolveu "Back to the Future" como título em português de
/// <i>De Volta Para o Futuro</i> — um apelido em inglês marcado como <c>pt</c>.
/// Por isso a consulta traz todos e a escolha é feita aqui, com regra: vale o
/// primeiro que DIFERE do título original. Um "apelido em português" idêntico
/// ao original não traduz nada e não é evidência de nome próprio no Brasil.
/// </summary>
public static class FilmeSparql
{
    /// <summary>Q7727 = minuto · Q11574 = segundo · Q25235 = hora.</summary>
    private const string Modelo = """
        SELECT ?obra
               (GROUP_CONCAT(DISTINCT ?ptbrA; separator="|") AS ?brApelidos)
               (SAMPLE(?ptbrL) AS ?brRotulo)
               (GROUP_CONCAT(DISTINCT ?ptA;   separator="|") AS ?ptApelidos)
               (SAMPLE(?ptL)   AS ?ptRotulo)
               (SAMPLE(?enL) AS ?ingles) (SAMPLE(?origL) AS ?original)
               (MIN(?ano) AS ?anoMin) (SAMPLE(?minutos) AS ?duracao)
               (GROUP_CONCAT(DISTINCT ?dirL;  separator="|") AS ?direcao)
               (GROUP_CONCAT(DISTINCT ?genL;  separator="|") AS ?generos)
               (GROUP_CONCAT(DISTINCT ?paisL; separator="|") AS ?paises)
        WHERE {
          ?obra wdt:P345 "@ID" .
          OPTIONAL { ?obra skos:altLabel ?ptbrA . FILTER(LANG(?ptbrA) = "pt-br") }
          OPTIONAL { ?obra rdfs:label    ?ptbrL . FILTER(LANG(?ptbrL) = "pt-br") }
          OPTIONAL { ?obra skos:altLabel ?ptA   . FILTER(LANG(?ptA)   = "pt")    }
          OPTIONAL { ?obra rdfs:label    ?ptL   . FILTER(LANG(?ptL)   = "pt")    }
          OPTIONAL { ?obra rdfs:label    ?enL   . FILTER(LANG(?enL)   = "en")    }
          OPTIONAL { ?obra wdt:P1476 ?origL }
          OPTIONAL { ?obra wdt:P577 ?data . BIND(YEAR(?data) AS ?ano) }
          OPTIONAL {
            ?obra p:P2047/psv:P2047 ?dv .
            ?dv wikibase:quantityAmount ?qtd ; wikibase:quantityUnit ?un .
            BIND(IF(?un = wd:Q11574, ?qtd/60, IF(?un = wd:Q25235, ?qtd*60, ?qtd)) AS ?minutos)
          }
          OPTIONAL { ?obra wdt:P57  ?d . ?d rdfs:label ?dirL  . FILTER(LANG(?dirL)  = "pt") }
          OPTIONAL { ?obra wdt:P136 ?g . ?g rdfs:label ?genL  . FILTER(LANG(?genL)  = "pt") }
          OPTIONAL { ?obra wdt:P495 ?p . ?p rdfs:label ?paisL . FILTER(LANG(?paisL) = "pt") }
        }
        GROUP BY ?obra
        LIMIT 1
        """;

    /// <summary>
    /// A consulta para um ID, ou vazio quando o ID não tem a forma.
    ///
    /// O ID entra por substituição de texto numa consulta, então ele é
    /// conferido AQUI e não confiado a quem chama — `tt` e dígitos não têm como
    /// carregar aspas, chave nem espaço.
    /// </summary>
    public static string Consulta(string? imdbId)
    {
        var id = ImdbId.Normalizar(imdbId);
        return id.Length == 0 ? string.Empty : Modelo.Replace("@ID", id);
    }

    /// <summary>
    /// O corpo da resposta → ficha. <c>null</c> quando o Wikidata não conhece
    /// o ID — que **não** é o mesmo que "o filme não existe".
    /// </summary>
    public static FilmeInfo? Ler(string imdbId, string json)
    {
        var id = ImdbId.Normalizar(imdbId);
        if (id.Length == 0 || string.IsNullOrWhiteSpace(json)) return null;

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
            var original = V(r, "original");
            var ingles = V(r, "ingles");

            // O APELIDO VEM ANTES DO RÓTULO — armadilha 3 do cabeçalho — e
            // entre os apelidos vale o primeiro que DIFERE do original: é essa
            // diferença que denuncia uma tradução. Um apelido idêntico ao
            // título original não traduziu nada.
            var br = Traduzido(Lista(r, "brApelidos"), original, ingles)
                     ?? Traduzido(V(r, "brRotulo"), original, ingles);
            var pt = Traduzido(Lista(r, "ptApelidos"), original, ingles)
                     ?? Traduzido(V(r, "ptRotulo"), original, ingles);

            return new FilmeInfo(
                id,
                br,
                pt,
                original,
                ingles,
                Ano(V(r, "anoMin")),
                Minutos(V(r, "duracao")),
                Lista(r, "direcao"),
                Lista(r, "generos"),
                Lista(r, "paises"),
                V(r, "obra") is { } uri ? uri.Split('/')[^1] : null,
                "Wikidata");
        }
    }

    private static string? V(JsonElement e, string chave) =>
        e.TryGetProperty(chave, out var n) && n.TryGetProperty("value", out var v)
            && v.GetString() is { Length: > 0 } s && !string.IsNullOrWhiteSpace(s)
            ? s.Trim()
            : null;

    private static string[]? Lista(JsonElement e, string chave) =>
        V(e, chave)?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            is { Length: > 0 } a ? a : null;

    /// <summary>
    /// O primeiro candidato que de fato TRADUZ — isto é, que não repete o
    /// título original nem o inglês.
    ///
    /// Devolver o candidato idêntico ao original seria afirmar "este é o nome
    /// no Brasil" sem nenhuma evidência de que seja: o campo pode estar
    /// preenchido com o que havia. `null` aqui é o caso comum e é resposta —
    /// a tela diz que não sabe, em vez de mostrar o inglês com etiqueta de
    /// português.
    /// </summary>
    private static string? Traduzido(IEnumerable<string>? candidatos, string? original, string? ingles)
    {
        if (candidatos is null) return null;
        foreach (var c in candidatos)
            if (!Igual(c, original) && !Igual(c, ingles)) return c;
        return null;
    }

    private static string? Traduzido(string? candidato, string? original, string? ingles) =>
        candidato is null ? null : Traduzido([candidato], original, ingles);

    /// <summary>
    /// "Igual o suficiente para não ser tradução".
    ///
    /// Igualdade exata é fraca demais aqui. Os apelidos <c>pt</c> de <i>Um
    /// Sonho de Liberdade</i> são "Shawshank Redemption" e "Os Condenados de
    /// Shawshank": o primeiro é o título original sem o artigo, e passaria por
    /// tradução num teste de igualdade — devolvendo o inglês com etiqueta de
    /// português, que é exatamente o que este arquivo inteiro evita.
    ///
    /// Por isso a comparação é sobre a forma dobrada (sem acento, sem
    /// pontuação, sem espaço) e por CONTINÊNCIA nos dois sentidos: um título
    /// contido no outro é variação do mesmo nome, não tradução dele.
    /// </summary>
    private static bool Igual(string? a, string? b)
    {
        var x = Dobrar(a);
        var y = Dobrar(b);
        if (x.Length == 0 || y.Length == 0) return false;
        return x.Contains(y, StringComparison.Ordinal) || y.Contains(x, StringComparison.Ordinal);
    }

    /// <summary>Minúsculas, sem acento e só letra e dígito.</summary>
    private static string Dobrar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var normal = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normal.Length);
        foreach (var ch in normal)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>O ano sai de um `xsd:integer` ou do começo de uma data.</summary>
    private static int? Ano(string? bruto)
    {
        if (bruto is null || bruto.Length < 4) return null;
        return int.TryParse(bruto[..4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
            && a is > 1870 and < 2200 ? a : null;
    }

    /// <summary>
    /// A duração já vem convertida pela consulta; aqui só se arredonda e se
    /// recusa o absurdo. O teto de 6.000 min (100 h) existe para o dia em que
    /// aparecer uma unidade que a consulta não conhece: melhor não mostrar
    /// duração do que mostrar "10809 min".
    /// </summary>
    private static int? Minutos(string? bruto) =>
        double.TryParse(bruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
        && d is > 0 and <= 6000 ? (int)Math.Round(d) : null;
}
