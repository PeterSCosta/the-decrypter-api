namespace TheDecrypter.Domain.Search;

/// <summary>
/// A forma de um código da CID-10, nas duas grafias que o mundo real usa.
///
/// O prontuário escreve <c>A00.0</c>; a base do SUS (SIH, SIM, SINAN) escreve
/// <c>A000</c>. São o mesmo código, e quem digita numa prova pode ter copiado de
/// qualquer um dos dois — então o banco guarda só a forma sem ponto, e o ponto
/// volta na exibição.
///
/// A regra de forma vive aqui, e não espalhada pelo controller: é ela que decide
/// se a entrada merece uma consulta, e a mesma resposta precisa valer para o
/// portão (<see cref="LookupShape"/>) e para o repositório.
/// </summary>
public static class CidCodigo
{
    /// <summary>
    /// A grafia canônica (sem ponto, maiúscula) — ou <c>null</c> se não é CID.
    ///
    /// Aceita <c>A00</c>, <c>A000</c>, <c>A00.0</c>, <c>a00.0</c> e tolera espaço
    /// e hífen no meio, que é o que sobra de um PDF copiado. Não aceita mais de
    /// um separador nem um separador fora do lugar: <c>A.000</c> não é código, é
    /// outra coisa que por acaso tem letras e dígitos.
    /// </summary>
    public static string? Normalizar(string? entrada)
    {
        var t = (entrada ?? string.Empty).Trim();
        if (t.Length is < 3 or > 6) return null;

        Span<char> limpo = stackalloc char[6];
        var n = 0;
        var separadores = 0;
        foreach (var ch in t)
        {
            if (ch is '.' or ' ' or '-')
            {
                // O separador só pode estar entre a categoria e a subcategoria.
                if (n != 3 || ++separadores > 1) return null;
                continue;
            }
            if (n == 6) return null;
            limpo[n++] = char.ToUpperInvariant(ch);
        }

        if (n is < 3 or > 4) return null;
        if (!char.IsAsciiLetter(limpo[0])) return null;
        for (var i = 1; i < n; i++)
            if (!char.IsAsciiDigit(limpo[i]))
                return null;

        return new string(limpo[..n]);
    }

    /// <summary>A grafia com ponto, que é como o código se lê: <c>A00.0</c>.</summary>
    public static string Exibir(string codigo) =>
        codigo.Length == 4 ? $"{codigo[..3]}.{codigo[3]}" : codigo;

    /// <summary>
    /// O padrão POSIX que acha uma doença pelo NOME — ancorado em início de
    /// palavra (<c>\m</c>), nunca substring solta.
    ///
    /// Com <c>%termo%</c>, "cola" acharia "Cólera" e "ebi" traria "flebite":
    /// numa bancada cuja entrada é texto qualquer, isso é ruído garantido.
    ///
    /// Os metacaracteres são neutralizados porque o termo vem de quem digita —
    /// um parêntese solto faria o Postgres recusar a expressão inteira, e o erro
    /// subiria como 500 em vez de "nada encontrado". Só o que não é letra nem
    /// dígito é escapado: escapar uma letra mudaria o sentido (<c>\d</c> vira
    /// classe de dígito).
    /// </summary>
    public static string PadraoDeNome(string termo)
    {
        var sb = new System.Text.StringBuilder(termo.Length * 2 + 2);
        sb.Append(@"\m");
        foreach (var ch in termo)
        {
            if (!char.IsLetterOrDigit(ch)) sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
