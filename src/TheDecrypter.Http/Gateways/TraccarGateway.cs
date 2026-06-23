using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using TheDecrypter.Domain.Gateways;

namespace TheDecrypter.Http.Gateways;

/// <summary>
/// Lê a frota do Traccar (self-hosted): GET /api/devices + /api/positions,
/// mesclados por deviceId. Token/URL vêm de Traccar:BaseUrl / Traccar:Token
/// (server-side). Sem config → retorna lista vazia (aba mostra "configure").
/// </summary>
public class TraccarGateway(HttpClient http, IConfiguration config) : IFleetGateway
{
    public async Task<IReadOnlyList<FleetDevice>> GetFleetAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config["Traccar:BaseUrl"]) ||
            string.IsNullOrWhiteSpace(config["Traccar:Token"]))
            return [];

        var devices = await http.GetFromJsonAsync<List<TraccarDevice>>("api/devices", ct) ?? [];
        var positions = await http.GetFromJsonAsync<List<TraccarPosition>>("api/positions", ct) ?? [];

        var latest = positions
            .GroupBy(p => p.DeviceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.FixTime).First());

        return devices.Select(d =>
        {
            latest.TryGetValue(d.Id, out var p);
            return new FleetDevice(
                d.Id,
                string.IsNullOrWhiteSpace(d.Name) ? $"#{d.Id}" : d.Name,
                d.Status ?? "unknown",
                // Idade da POSIÇÃO (fix GPS), não do último ping do device — assim o
                // "visto há X" reflete há quanto tempo a localização é de verdade.
                p?.FixTime ?? d.LastUpdate,
                p?.Latitude,
                p?.Longitude,
                p is null ? null : Math.Round(p.Speed * 1.852, 1), // nós → km/h
                p?.Course,
                p?.Attributes?.BatteryLevel is { } b ? (int)Math.Round(b) : null,
                p?.Attributes?.Motion ?? false);
        }).ToList();
    }

    // JSON do Traccar é camelCase; GetFromJsonAsync usa Web defaults (case-insensitive).
    private record TraccarDevice(long Id, string? Name, string? Status, DateTime? LastUpdate);

    private record TraccarPosition(
        long DeviceId, double Latitude, double Longitude, double Speed, double Course,
        DateTime FixTime, TraccarAttributes? Attributes);

    private record TraccarAttributes(double? BatteryLevel, bool? Motion);
}
