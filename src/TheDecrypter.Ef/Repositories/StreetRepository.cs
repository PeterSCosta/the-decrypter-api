using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Ef.Repositories;

public class StreetRepository(DecrypterDbContext db) : IStreetRepository
{
    public async Task<IReadOnlyList<Street>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        var q = query.Trim();
        if (q.Length == 0) return [];
        limit = Math.Clamp(limit, 1, 200);

        // Data da Lei (tem "/")
        if (q.Contains('/'))
        {
            var needle = q.Replace(" ", "");
            return await db.Streets
                .Where(s => s.DataLei != null && EF.Functions.Like(s.DataLei, $"%{needle}%"))
                .Take(limit).ToListAsync(ct);
        }

        // Número → código ou Nº da Lei
        if (int.TryParse(q, out var n))
        {
            return await db.Streets
                .Where(s => s.Codigo == n || s.NumLei == n)
                .Take(limit).ToListAsync(ct);
        }

        // Texto → nome (ignora acentos)
        var pattern = $"%{q}%";
        return await db.Streets
            .FromSql($"SELECT * FROM street WHERE unaccent(nome) ILIKE unaccent({pattern}) ORDER BY length(nome) LIMIT {limit}")
            .ToListAsync(ct);
    }
}
