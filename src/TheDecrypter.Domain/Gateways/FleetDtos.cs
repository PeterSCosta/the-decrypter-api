namespace TheDecrypter.Domain.Gateways;

/// <summary>
/// Um dispositivo da frota (Traccar) com a última posição. Velocidade já em km/h.
/// </summary>
public record FleetDevice(
    long Id,
    string Name,
    string Status,           // online | offline | unknown
    DateTime? LastUpdate,
    double? Lat,
    double? Lng,
    double? SpeedKmh,
    double? Course,          // rumo em graus
    int? Battery,            // %
    bool Moving,
    string? Phone);          // telefone do device no Traccar (se informado)
