namespace TheDecrypter.Domain.Entities;

/// <summary>
/// Rol de Ruas completo — as 4.426 linhas, não as 4.011 que sobram quando
/// `codigo` é chave. Ver o comentário de `street_rol` no schema.
/// </summary>
public class StreetRol
{
    public int Id { get; set; }
    public int Codigo { get; set; }
    public string Tipo { get; set; } = default!;
    public string Nome { get; set; } = default!;
    public int? BairroNum { get; set; }
    public string? Bairro { get; set; }
    public int? NumLei { get; set; }
    public string? DataLei { get; set; }
    public string? Localizacao { get; set; }
    public double? Ext { get; set; }
    public double? Larg { get; set; }
    public string? Atas { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}
