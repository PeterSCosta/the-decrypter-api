using System.Globalization;
using System.Text.Json;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>Geocodificação via Nominatim/OSM (User-Agent obrigatório; rate baixo).</summary>
public class NominatimGateway(HttpClient http) : IGeocodeGateway
{
    public async Task<GeocodeInfo?> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = query.Trim();
        if (q.Length < 3) return null;

        using var resp = await http.GetAsync(
            $"search?q={Uri.EscapeDataString(q)}&format=jsonv2&limit=1&countrycodes=br", ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        var first = doc.RootElement[0];
        if (!double.TryParse(first.GetProperty("lat").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lat))
            return null;
        if (!double.TryParse(first.GetProperty("lon").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
            return null;

        return new GeocodeInfo(q, lat, lng,
            first.TryGetProperty("display_name", out var dn) ? dn.GetString() : null);
    }
}
