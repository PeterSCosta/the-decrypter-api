using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

public interface IAirportRepository
{
    /// <summary>IATA (3 letras) ou ICAO (4). Decide pelo comprimento.</summary>
    Task<Airport?> ByCodeAsync(string code, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
