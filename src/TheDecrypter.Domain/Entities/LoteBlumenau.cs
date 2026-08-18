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
}
