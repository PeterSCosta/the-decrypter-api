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
