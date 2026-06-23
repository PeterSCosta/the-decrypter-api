using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Ef.Repositories;

public class MunicipioRepository(DecrypterDbContext db) : IMunicipioRepository
{
    public async Task<Municipio?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length == 7 && int.TryParse(digits, out var c7))
            return await db.Municipios.FirstOrDefaultAsync(m => m.CodigoIbge == c7, ct);
        // 6 dígitos = código sem o dígito verificador (primeiros 6)
        if (digits.Length == 6 && int.TryParse(digits, out var c6))
            return await db.Municipios.FirstOrDefaultAsync(m => m.CodigoIbge / 10 == c6, ct);
        return null;
    }

    public async Task<IReadOnlyList<Municipio>> SearchByNameAsync(string name, int limit, CancellationToken ct = default)
    {
        var q = name.Trim();
        if (q.Length < 3) return [];
        var pattern = $"%{q}%";
        return await db.Municipios
            .FromSql($"SELECT * FROM municipio WHERE unaccent(nome) ILIKE unaccent({pattern}) ORDER BY nome LIMIT {Math.Clamp(limit, 1, 50)}")
            .ToListAsync(ct);
    }
}
