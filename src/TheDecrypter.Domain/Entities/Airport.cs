namespace TheDecrypter.Domain.Entities;

/// <summary>Aeroporto (OpenFlights). Código vazio = a base de origem não tem.</summary>
public class Airport
{
    /// <summary>Sintética: a base de origem não tem chave própria.</summary>
    public int Id { get; set; }

    public string? Iata { get; set; }
    public string? Icao { get; set; }
    public string? Nome { get; set; }
    public string? Cidade { get; set; }
    public string? Pais { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
}
