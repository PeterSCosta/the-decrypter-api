using System.Globalization;
using System.Text;
using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Export;

/// <summary>
/// A lista de CEPs virando arquivo.
///
/// POR QUE NO DOMAIN, E NÃO NO CONTROLLER: o projeto de teste não referencia o
/// `TheDecrypter.Ef` nem o `TheDecrypter.Api` — o que mora no controller não tem
/// como ser testado aqui. É o mesmo caminho que o `CepPattern` percorreu, pelo
/// mesmo motivo escrito lá.
///
/// O ARQUIVO É PARA O EXCEL EM PT-BR, e é isso que decide cada escolha abaixo.
/// Um CSV "correto" que abre com tudo numa coluna só e com "São José" escrito
/// errado não serve para nada, e quem baixa não vai culpar o Excel.
/// </summary>
public static class CepCsv
{
    /// <summary>
    /// Ponto-e-vírgula, e não vírgula.
    ///
    /// O Excel não lê o separador do arquivo: ele usa o separador de lista do
    /// SISTEMA, que em pt-BR é `;`. Um CSV com vírgula abre com as sete colunas
    /// empilhadas numa só. A vírgula fica livre para ser o separador decimal,
    /// que é o que ela é aqui.
    /// </summary>
    public const char Separador = ';';

    /// <summary>
    /// "Município" é a coluna de <see cref="Cep.Localidade"/> — não é engano.
    ///
    /// O seeder gravou o NOME DO MUNICÍPIO no campo `localidade` e o bairro em
    /// `bairro`; o front desfaz isso na leitura (`daApi`, em `lookups.ts`). Num
    /// CSV não existe a coluna vizinha para desmentir um rótulo errado, então o
    /// cabeçalho diz o que o dado É, não como a coluna se chama no banco.
    /// </summary>
    public const string Cabecalho = "CEP;Logradouro;Bairro;Município;UF;Latitude;Longitude";

    /// <summary>RFC 4180, e o que o Excel espera.</summary>
    private const string FimDeLinha = "\r\n";

    /// <summary>
    /// A vírgula decimal, escrita à mão.
    ///
    /// NÃO troque por `CultureInfo.GetCultureInfo("pt-BR")`: o
    /// `Directory.Build.props` liga `InvariantGlobalization`, e nesse modo a
    /// cultura pedida volta INVARIANTE em silêncio — sem exceção, sem aviso. A
    /// coordenada sairia com ponto, o Excel pt-BR a leria como texto, e o
    /// arquivo pareceria certo até alguém tentar ordenar por latitude.
    /// </summary>
    private static readonly NumberFormatInfo Numero = new() { NumberDecimalSeparator = "," };

    /// <summary>Um CSV pronto para baixar: BOM, cabeçalho e uma linha por CEP.</summary>
    public static byte[] Gerar(IReadOnlyList<Cep> linhas)
    {
        var sb = new StringBuilder(64 + (linhas.Count * 72));
        sb.Append(Cabecalho).Append(FimDeLinha);
        foreach (var c in linhas) sb.Append(Linha(c)).Append(FimDeLinha);

        // BOM UTF-8. Sem ele o Excel pt-BR abre o arquivo como ANSI e todo
        // "Município"/"São José"/"Herval d'Oeste" vira caractere trocado. É a
        // diferença entre um arquivo certo e um arquivo que PARECE corrompido.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    /// <summary>Uma linha do arquivo, na ordem do <see cref="Cabecalho"/>.</summary>
    public static string Linha(Cep c) => string.Join(
        Separador,
        Campo(FormatarCep(c.Code)),
        Campo(c.Logradouro),
        Campo(c.Bairro),
        // Vazio fica vazio: 86 dos 40.445 CEPs da base não têm nome de
        // município na origem (o índice 2 do `municipios[]` do seed é ""). Um
        // travessão no lugar seria inventar dado num arquivo que vai ser lido
        // como fonte; célula vazia é o que "não sei" parece numa planilha.
        Campo(c.Localidade),
        // `char(2)` no schema volta com espaço de enchimento — "SC " numa
        // planilha não casa com "SC" em nenhum PROCV.
        Campo(c.Uf?.Trim()),
        Coordenada(c.Lat),
        Coordenada(c.Lng));

    /// <summary>"88010500" → "88010-500".</summary>
    private static string FormatarCep(string? code) =>
        code is { Length: 8 } ? $"{code[..5]}-{code[5..]}" : code ?? string.Empty;

    /// <summary>Número com vírgula decimal; nulo vira célula vazia.</summary>
    private static string Coordenada(double? v) =>
        v?.ToString(Numero) ?? string.Empty;

    /// <summary>
    /// Um campo de texto, escapado.
    ///
    /// Duas defesas, e a segunda não é paranoia de manual: aspas NÃO desarmam
    /// fórmula. O Excel tira as aspas na leitura e avalia o que começa com
    /// `= + - @` do mesmo jeito — um arquivo que circula entre as equipes não
    /// pode carregar célula executável. Hoje nenhuma das 40.445 linhas dispara
    /// isto (medido: zero campos com pontuação fora do apóstrofo de "Herval
    /// d'Oeste"); a guarda existe para o dia em que a base virar nacional, que é
    /// o dia em que ninguém vai lembrar de acrescentá-la.
    /// </summary>
    private static string Campo(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;

        var v = valor;
        if (v[0] is '=' or '+' or '-' or '@' or '\t' or '\r') v = $"'{v}";

        return v.Any(ch => ch == Separador || ch is '"' or '\r' or '\n')
            ? $"\"{v.Replace("\"", "\"\"")}\""
            : v;
    }

    /// <summary>
    /// O nome do arquivo que a pessoa vai achar na pasta de downloads.
    ///
    /// Todo curinga vira `x`: `*` e `?` são inválidos em nome de arquivo no
    /// Windows, e `88010-5x0` e `88010-5?0` são o MESMO padrão — dois nomes
    /// diferentes para o mesmo arquivo só atrapalham quem compara com o colega.
    /// Este nome sai no `Content-Disposition` e o front o lê de lá: a regra é
    /// escrita uma vez, aqui.
    /// </summary>
    public static string NomeDoArquivo(string padrao)
    {
        var limpo = new string([.. padrao
            .Where(ch => char.IsDigit(ch) || Search.CepPattern.Curingas.Contains(ch))
            .Select(ch => char.IsDigit(ch) ? ch : 'x')]);
        return $"ceps-{limpo}.csv";
    }
}
