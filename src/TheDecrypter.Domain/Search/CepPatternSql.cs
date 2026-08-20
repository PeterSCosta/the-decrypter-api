using System.Text;

namespace TheDecrypter.Domain.Search;

/// <summary>Padrão de CEP traduzido para o que o Postgres entende.</summary>
/// <param name="Like">Prefixo fixo para o índice (`code LIKE`).</param>
/// <param name="Regex">Regex POSIX para o `~`.</param>
/// <param name="Ancorado">Padrão de 8 posições (máscara) ou substring.</param>
public record CepPatternSql(string Like, string Regex, bool Ancorado);

/// <summary>
/// Tradução de padrão com curinga para SQL.
///
/// POR QUE ISTO SAIU DO REPOSITÓRIO: esta é a **única** regra que precisa ser
/// idêntica em dois idiomas — o `cep-pattern.ts` do app e o SQL daqui. Enterrada
/// na camada Ef ela era intestável, e a divergência aparecia como "a busca com
/// curinga acha coisa diferente dependendo de onde roda".
///
/// A regra (a mesma do TS): dígito casa literalmente; `x X * _ ?` são curinga de
/// um dígito; espaço, ponto e traço são ignorados. **Oito caracteres viram
/// máscara ancorada** (`88xxx500` → `^88\d\d\d500$`); **menos que isso é
/// substring** (`500` casa qualquer CEP que contenha 500). Era exatamente aqui
/// que o C# divergia: ele ancorava sempre, então `500` não achava nada.
/// </summary>
public static class CepPattern
{
    /// <summary>Curinga de um dígito. A mesma lista do `WILDCARD` do TS.</summary>
    public const string Curingas = "xX*_?";

    /// <summary>O que o padrão ignora: espaço, ponto e traço ("88010-500").</summary>
    public static bool ESeparador(char c) => char.IsWhiteSpace(c) || c is '.' or '-';

    public static CepPatternSql? Traduzir(string bruto)
    {
        // Separador some; qualquer outra coisa RECUSA o padrão inteiro.
        //
        // Recusar — e não descartar — é o ponto. O TS sempre recusou
        // (`if (!/^[0-9xX*_?]+$/.test(cleaned)) return null`); aqui a limpeza
        // era um `Where` que JOGAVA FORA as letras, então "Rua XV" virava o
        // padrão "X" (= `\d`, que casa com todo CEP) em vez de virar `null`.
        // Era a divergência entre os dois idiomas que este arquivo existe para
        // impedir, e ela custava a busca de rua da principal de Blumenau.
        var limpo = new string([.. bruto.Where(c => !ESeparador(c))]);
        if (limpo.Length is 0 or > 8) return null;
        if (limpo.Any(c => !char.IsDigit(c) && !Curingas.Contains(c))) return null;

        var regex = new StringBuilder();
        var prefixo = new StringBuilder();
        var prefixoAberto = true;
        foreach (var ch in limpo)
        {
            if (char.IsDigit(ch))
            {
                regex.Append(ch);
                if (prefixoAberto) prefixo.Append(ch);
            }
            else
            {
                regex.Append("\\d");
                prefixoAberto = false;
            }
        }

        var ancorado = limpo.Length == 8;
        // Só o padrão ancorado tem prefixo útil para o índice: numa busca por
        // substring o começo do CEP é livre, e um LIKE com prefixo excluiria
        // justamente os acertos do meio.
        var like = ancorado && prefixo.Length > 0 ? $"{prefixo}%" : "%";
        var rx = ancorado ? $"^{regex}$" : regex.ToString();
        return new CepPatternSql(like, rx, ancorado);
    }
}
