namespace TheDecrypter.Domain.Gateways;

/// <summary>Geocodificação de endereço/CEP → coordenada (Nominatim/OSM).</summary>
public interface IGeocodeGateway
{
    Task<GeocodeInfo?> SearchAsync(string query, CancellationToken ct = default);
}
