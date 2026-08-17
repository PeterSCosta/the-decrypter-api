namespace TheDecrypter.Domain.Entities;

/// <summary>Ponte, passarela ou viaduto nomeado (lei municipal + OpenStreetMap).</summary>
public class Bridge
{
    public int Id { get; set; }
    public string Nome { get; set; } = default!;
    public string? NomeOsm { get; set; }
    /// <summary>Apelidos juntados por " · " — é como as pessoas chamam.</summary>
    public string? Apelidos { get; set; }
    public string? Tipo { get; set; }
    public string? Fonte { get; set; }
    public string? Lei { get; set; }
    public int? NumLei { get; set; }
    public int? AnoLei { get; set; }
    public string? DataLei { get; set; }
    public string? Ementa { get; set; }
    public string? UrlLei { get; set; }
    public string? Situacao { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public double? Comprimento { get; set; }
    public string? Via { get; set; }
    public string? Material { get; set; }
    public string? Transpoe { get; set; }
    public string? Bairros { get; set; }
}
