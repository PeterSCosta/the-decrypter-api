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
