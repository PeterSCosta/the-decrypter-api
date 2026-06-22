namespace TheDecrypter.Domain.Entities;

/// <summary>CEP (8 dígitos) com logradouro/bairro/cidade e coordenada.</summary>
public class Cep
{
    public string Code { get; set; } = default!;
    public string? Logradouro { get; set; }
    public string? Bairro { get; set; }
    public string? Localidade { get; set; }
    public int? MunicipioIbge { get; set; }
    public string Uf { get; set; } = default!;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}
