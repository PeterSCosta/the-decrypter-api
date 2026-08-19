namespace TheDecrypter.Domain.Entities;

/// <summary>
/// Um lote do cadastro imobiliário de Blumenau.
///
/// A `Lat`/`Lng` é o CENTROIDE do terreno, não a porta — a diferença é de
/// metros num lote urbano, mas quem manda alguém até o local precisa saber.
/// </summary>
public class LoteBlumenau
{
    /// <summary>
    /// Sintética: 48 lotes da camada vêm SEM inscrição e 6 inscrições se
    /// repetem, então ela não serve de chave — e descartar essas linhas seria
    /// esconder lote real.
    /// </summary>
    public int Id { get; set; }

    /// <summary>15 dígitos, com os zeros à esquerda: `412400200002000`.</summary>
    public string? Inscricao { get; set; }

    /// <summary>A mesma coisa com hífen e sem zeros: `4-1-24-20-2`.</summary>
    public string? Iq { get; set; }

    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Cep { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int? AreaM2 { get; set; }

    /// <summary>
    /// O conjunto de endereços do lote, quando ele não cabe em
    /// `Logradouro` + `Numero`: `"7 DE SETEMBRO, 1560;DOUTOR AMADEU DA LUZ, 241"`.
    ///
    /// O lote de ESQUINA tem mais de uma porta, e a camada de lotes só guarda
    /// uma — a tabela de endereços do geoportal guarda todas. Escolher uma
    /// apagaria justamente a que uma prova usa ("a casa da esquina da X com a
    /// Y").
    ///
    /// Nulo no caso comum, de um endereço só: ali o número já está em `Numero`,
    /// e repetir a mesma coisa em duas colunas é convite a divergirem. Ele
    /// também é preenchido quando o único endereço colhido é de OUTRA rua —
    /// esse não pode subir para `Numero` sem mudar o lote de rua.
    /// </summary>
    public string? Enderecos { get; set; }
}
