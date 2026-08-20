namespace TheDecrypter.Domain.Search;

/// <summary>
/// A forma de um ID da IMDb — <c>tt</c> e 7 ou 8 dígitos.
///
/// Mora no Domain porque DOIS lugares precisam concordar: o portão de forma
/// (<c>LookupShape</c>, que decide se vale perguntar) e o gateway (que
/// interpola o ID numa consulta SPARQL e por isso não pode confiar em ninguém).
/// Escrever a mesma regra duas vezes é o caminho curto para elas divergirem —
/// e a segunda cópia é justamente a que protege a consulta.
/// </summary>
public static class ImdbId
{
    public static bool Parece(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        if (t.Length is < 9 or > 10) return false;
        if (!t.StartsWith("tt", StringComparison.OrdinalIgnoreCase)) return false;
        return t[2..].All(char.IsAsciiDigit);
    }

    /// <summary>Forma canônica: minúsculas, sem espaço. Vazio quando não é ID.</summary>
    public static string Normalizar(string? s) =>
        Parece(s) ? s!.Trim().ToLowerInvariant() : string.Empty;
}

/// <summary>
/// A forma de um item do Wikidata — <c>Q</c> e dígitos.
///
/// ── POR QUE ELE É UMA SEGUNDA PORTA, E NÃO RUÍDO ──────────────────────────
/// O `Q4941` é o mesmo filme que o `tt1074638`, escrito no catálogo do
/// Wikidata em vez do da IMDb — e quem copia de uma página do Wikidata copia
/// este. A bancada lê `Q4941` como cauda de Geohash e devolve cinco pontos em
/// Blumenau; medido, 61% dos QIDs sorteados fazem isso.
///
/// A cauda vale <c>0,52</c> justamente porque é palpite entre cinco leituras,
/// todas assumindo um prefixo de cidade. Um acerto EXATO numa base real é
/// evidência de outra natureza, e a regra da casa já diz qual ganha. As duas
/// leituras continuam na tela; o que muda é a ordem.
///
/// ── E POR QUE ISSO NÃO CONTRADIZ A REGRA DO QID ───────────────────────────
/// A regra escrita é que o QID nunca vira valor **clicável, encadeável ou
/// copiável** — ela é sobre o QID como SAÍDA, porque encadeá-lo joga a próxima
/// volta na leitura de coordenada. Como ENTRADA ele é uma chave legítima, e
/// resolvê-lo é o oposto de propagar o engano: é encerrá-lo.
/// </summary>
public static class WikidataId
{
    public static bool Parece(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        if (t.Length is < 2 or > 12) return false;
        if (t[0] is not ('Q' or 'q')) return false;
        return t[1..].All(char.IsAsciiDigit) && t[1] != '0';
    }

    /// <summary>Forma canônica: `Q` maiúsculo. Vazio quando não é QID.</summary>
    public static string Normalizar(string? s) =>
        Parece(s) ? $"Q{s!.Trim()[1..]}" : string.Empty;
}

/// <summary>
/// A CHAVE DE UM FILME — num lugar só, e é por isso que ela existe.
///
/// ── O DEFEITO QUE ESTA CLASSE CONSERTA ────────────────────────────────────
/// Havia dois lugares conferindo a forma da chave: o portão
/// (<c>LookupShape</c>, que decide se vale perguntar) e o serviço
/// (<c>LookupService.FilmeAsync</c>, que decide o que mandar ao gateway).
/// Quando o `Q4941` virou segunda porta, o primeiro aprendeu e o segundo não —
/// e o resultado foi o pior tipo de falha: a bancada abria a consulta, o
/// serviço a descartava caladamente antes de sair, e a tela mostrava só a
/// leitura de coordenada. Nada quebrou; a resposta simplesmente não veio.
///
/// Duas cópias de uma regra divergem, e a que sobrevive é a errada. Agora é
/// uma função, e quem precisar da forma chama esta.
/// </summary>
public static class ChaveDeFilme
{
    /// <summary>`tt1074638`, `Q4941`, `P345` ou `L1`, na forma canônica.</summary>
    public static string Normalizar(string? s)
    {
        var tt = ImdbId.Normalizar(s);
        return tt.Length > 0 ? tt : CodigoWikidata.Normalizar(s);
    }

    public static bool Parece(string? s) => Normalizar(s).Length > 0;
}

/// <summary>As três espécies de código que o Wikidata usa.</summary>
public enum EspecieWikidata
{
    /// <summary>`Q…` — uma COISA: filme, pessoa, planeta, cidade.</summary>
    Item,
    /// <summary>`P…` — o CAMPO em si: `P345` é "identificador IMDb".</summary>
    Propriedade,
    /// <summary>`L…` — uma PALAVRA, com língua, classe gramatical e sentidos.</summary>
    Lexema,
}

/// <summary>
/// A forma de um código do Wikidata, nas três espécies.
///
/// Nem tudo lá começa com `Q`, e a diferença importa: `Q2` é a Terra, `P345` é
/// o campo "identificador IMDb" e `L1` é a palavra "ama". São perguntas
/// diferentes, respondidas por consultas diferentes — mas todas EXATAS, porque
/// um código aponta para um registro e só um.
/// </summary>
public static class CodigoWikidata
{
    public static EspecieWikidata? Especie(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        if (t.Length is < 2 or > 12) return null;
        // Sem zero à esquerda: `Q0` e `P01` não existem, e aceitá-los faria a
        // bancada gastar requisição com forma que nunca resolve.
        if (t[1] == '0' || !t[1..].All(char.IsAsciiDigit)) return null;
        return char.ToUpperInvariant(t[0]) switch
        {
            'Q' => EspecieWikidata.Item,
            'P' => EspecieWikidata.Propriedade,
            'L' => EspecieWikidata.Lexema,
            _ => null,
        };
    }

    /// <summary>Forma canônica: prefixo maiúsculo. Vazio quando não é código.</summary>
    public static string Normalizar(string? s) =>
        Especie(s) is null ? string.Empty : $"{char.ToUpperInvariant(s!.Trim()[0])}{s.Trim()[1..]}";
}