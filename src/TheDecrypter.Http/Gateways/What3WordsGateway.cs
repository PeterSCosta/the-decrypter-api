using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>what3words → coordenada. Chave server-side (Gateways:What3Words:Key).</summary>
public class What3WordsGateway(HttpClient http, IConfiguration config) : IWhat3WordsGateway
{
    public async Task<W3wInfo?> ConvertAsync(string words, CancellationToken ct = default)
    {
        var key = config["Gateways:What3Words:Key"];
        if (string.IsNullOrWhiteSpace(key)) return null;

        var w = words.Trim().TrimStart('/').ToLowerInvariant();
        // Chave no header X-Api-Key (recomendação da doc w3w) em vez de query-string,
        // que vaza em logs de proxy/CDN.
        using var req = new HttpRequestMessage(
            HttpMethod.Get, $"v3/convert-to-coordinates?words={Uri.EscapeDataString(w)}");
        req.Headers.Add("X-Api-Key", key);
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        var r = doc.RootElement;
        if (!r.TryGetProperty("coordinates", out var c) || c.ValueKind != JsonValueKind.Object) return null;

        return new W3wInfo(
            w,
            c.GetProperty("lat").GetDouble(),
            c.GetProperty("lng").GetDouble(),
            r.TryGetProperty("nearestPlace", out var np) ? np.GetString() : null,
            r.TryGetProperty("country", out var co) ? co.GetString() : null);
    }
}
