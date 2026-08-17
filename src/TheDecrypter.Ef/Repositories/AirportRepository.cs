using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Ef.Repositories;

public class AirportRepository(DecrypterDbContext db) : IAirportRepository
{
    public Task<Airport?> ByCodeAsync(string code, CancellationToken ct = default)
    {
        var c = code.Trim().ToUpperInvariant();
        return c.Length switch
        {
            3 => db.Airports.FirstOrDefaultAsync(a => a.Iata == c, ct),
            4 => db.Airports.FirstOrDefaultAsync(a => a.Icao == c, ct),
            _ => Task.FromResult<Airport?>(null),
        };
    }

    public Task<int> CountAsync(CancellationToken ct = default) => db.Airports.CountAsync(ct);
}
