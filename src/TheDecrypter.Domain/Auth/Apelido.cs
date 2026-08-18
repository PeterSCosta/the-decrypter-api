using System.Text.RegularExpressions;

namespace TheDecrypter.Domain.Auth;

/// <summary>
/// A regra do apelido — e, com ela, a regra que decide se o que a pessoa
/// digitou no login é apelido ou e-mail.
///
/// ── POR QUE O `@` É PROIBIDO NO APELIDO ─────────────────────────────────────
/// O login tem UM campo só ("Apelido ou e-mail"), e o servidor precisa decidir
/// em qual coluna procurar. Todo e-mail contém `@` — a validação do cadastro
/// sempre exigiu isso — e nenhum apelido pode conter. Os dois conjuntos ficam
/// disjuntos, o roteador vira total, e some a única forma de um apelido imitar
/// o e-mail de outra pessoa.
///
/// ── A ÂNCORA DO PADRÃO NÃO É DETALHE DE ESTILO ──────────────────────────────
/// `Regex.IsMatch` procura SUBSTRING em .NET: sem `^…$`, "pеter" com um `е`
/// cirílico no meio passaria, porque o "p" sozinho já casa. O padrão ancorado é
/// o que faz o conjunto ASCII valer de verdade — e é ele que mata homóglifo sem
/// precisar de normalização Unicode.
/// </summary>
public static class Apelido
{
    public const int Minimo = 3;
    public const int Maximo = 24;

    /// <summary>
    /// Minúsculas, começando por letra ou dígito, e só ASCII depois.
    ///
    /// O conjunto ser ASCII puro dá um brinde: `lower()` do Postgres e
    /// `ToLowerInvariant()` do .NET não podem divergir por collation, então o
    /// índice único e a normalização do serviço nunca discordam.
    /// </summary>
    private static readonly Regex Formato =
        new(@"^[a-z0-9][a-z0-9._-]{2,23}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Nomes que confundem quem aprova.
    ///
    /// Não é escalonamento de privilégio — o papel vive na claim do token e o
    /// portão é `[Authorize(Roles = …)]`, então um apelido "admin" não dá poder
    /// nenhum. É a tela de aprovação: o admin decide olhando uma lista, e um
    /// pendente chamado "suporte" é engano fácil de cometer às 23h.
    /// </summary>
    private static readonly HashSet<string> Reservados =
    [
        "admin", "administrador", "root", "suporte", "sistema", "contato", "api", "null", "undefined",
    ];

    /// <summary>Vazio vira `null`: a coluna guarda NULL, nunca string vazia.</summary>
    public static string? Normalizar(string? bruto)
    {
        var t = (bruto ?? string.Empty).Trim().ToLowerInvariant();
        return t.Length == 0 ? null : t;
    }

    public static bool Valido(string apelidoNormalizado) => Formato.IsMatch(apelidoNormalizado);

    public static bool EhReservado(string apelidoNormalizado) =>
        Reservados.Contains(apelidoNormalizado);

    /// <summary>
    /// O roteador do login: `@` significa e-mail, e nada mais significa.
    ///
    /// Total e disjunto por construção — ver o cabeçalho desta classe.
    /// </summary>
    public static bool PareceEmail(string identificador) => identificador.Contains('@');

    /// <summary>A frase que a tela mostra quando o formato não passa.</summary>
    public const string RegraEmPalavras =
        "O apelido precisa ter de 3 a 24 caracteres, começar com letra ou número e usar só " +
        "letras sem acento, números, ponto, hífen ou sublinhado. Sem @.";
}
