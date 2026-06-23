namespace TheDecrypter.Domain.Entities;

/// <summary>Logradouro do Rol de Ruas (Blumenau): código, lei, data, nome.</summary>
public class Street
{
    public int Codigo { get; set; }
    public string Tipo { get; set; } = default!;
    public string Nome { get; set; } = default!;
    public string? Bairro { get; set; }
    public int? NumLei { get; set; }
    public string? DataLei { get; set; }
    public string? Localizacao { get; set; }
    public double? Ext { get; set; }   // extensão em metros (pode ser fracionária)
    public double? Larg { get; set; }
}
