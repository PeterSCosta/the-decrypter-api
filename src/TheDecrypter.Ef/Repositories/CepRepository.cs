using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Ef.Repositories;

public class CepRepository(DecrypterDbContext db) : ICepRepository
{
    public async Task<(IReadOnlyList<Cep>, int)> SearchWildcardAsync(
        string pattern, int limit, CancellationToken ct = default)
    {
        // A tradução do padrão mora em `CepPattern` (domínio), compartilhada com
        // o teste que a confere contra a especificação do app.
        if (CepPattern.Traduzir(pattern) is not { } p) return ([], 0);

        // O apelido "Value" NÃO é enfeite: o EF embrulha esta consulta escalar
        // num `SELECT s."Value" FROM (…) AS s`, e sem ele o Postgres responde
        // `42703: column s.Value does not exist`. O curinga é o único caminho
        // que passa por aqui, e ele dispara com qualquer `x` na entrada — então
        // o erro derrubava a consulta inteira da bancada para toda palavra com
        // "x", com o chip de "consultas online indisponíveis" no lugar da
        // resposta.
        var total = await db.Database
            .SqlQuery<int>(
                $@"SELECT count(*)::int AS ""Value"" FROM cep
                   WHERE code LIKE {p.Like} AND code ~ {p.Regex}")
            .FirstAsync(ct);
        var hits = await db.Ceps
            .FromSql($"SELECT * FROM cep WHERE code LIKE {p.Like} AND code ~ {p.Regex} ORDER BY code LIMIT {limit}")
            .AsNoTracking()
            .ToListAsync(ct);
        return (hits, total);
    }

    public async Task<Cep?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length != 8) return null;
        return await db.Ceps.AsNoTracking().FirstOrDefaultAsync(x => x.Code == digits, ct);
    }
}
