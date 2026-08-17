namespace TheDecrypter.Domain.Entities;

/// <summary>
/// Ponto de iluminação pública de Blumenau (portal Cidade Iluminada).
///
/// `coord_bnu` **não** é mapeada de propósito: é coluna gerada, e o Postgres
/// recusa qualquer INSERT que a mencione. Ela existe só para o índice de
/// proximidade; o `FromSql` com `SELECT *` ignora a coluna extra que volta.
/// </summary>
public class Poste
{
    public int Id { get; set; }

    /// <summary>Texto, não número: há plaqueta com zero à esquerda e uma com letra.</summary>
    public string? Plaqueta { get; set; }

    public double Lat { get; set; }
    public double Lng { get; set; }

    public string? Rua { get; set; }
    public string? RuaTipo { get; set; }
    public string? RuaNome { get; set; }
    public int? RuaId { get; set; }
    public int? Numero { get; set; }
    public string? Bairro { get; set; }

    /// <summary>A luminária: braço, tipo e lâmpada. Só 29% dos postes têm.</summary>
    public string? Estrutura { get; set; }
    public int? EstruturaId { get; set; }

    public string? Tipo { get; set; }
    public string? Status { get; set; }
    public short? PontosLuminosos { get; set; }
    public int? Altura { get; set; }
    public DateTimeOffset? Instalacao { get; set; }
    public DateTimeOffset? Alteracao { get; set; }
    public int? Cor { get; set; }

    /// <summary>Preenchida só nas consultas por proximidade. Não é coluna.</summary>
    public double? DistanciaMetros { get; set; }
}
