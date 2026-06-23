using System.Text;
using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;

namespace TheDecrypter.Ef.Repositories;

public class CepRepository(DecrypterDbContext db) : ICepRepository
{
    private const string Wildcards = "xX*_?";

    public async Task<IReadOnlyList<Cep>> SearchWildcardAsync(
        string pattern, int limit, CancellationToken ct = default)
    {
        var cleaned = new string(pattern.Where(c => char.IsDigit(c) || Wildcards.Contains(c)).ToArray());
        if (cleaned.Length is 0 or > 8) return [];

        // Prefixo fixo (dígitos iniciais) → usa o índice via LIKE; o padrão
        // completo (com curingas → \d) vira um regex POSIX do Postgres (`~`).
        var regex = new StringBuilder("^");
        var prefix = new StringBuilder();
        var prefixOpen = true;
        foreach (var ch in cleaned)
        {
            if (char.IsDigit(ch))
            {
                regex.Append(ch);
                if (prefixOpen) prefix.Append(ch);
            }
            else
            {
                regex.Append("\\d");
                prefixOpen = false;
            }
        }
        regex.Append('$');

        var like = (prefix.Length > 0 ? prefix.ToString() : "") + "%";
        var rx = regex.ToString();

        return await db.Ceps
            .FromSql($"SELECT * FROM cep WHERE code LIKE {like} AND code ~ {rx} ORDER BY code LIMIT {limit}")
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Cep?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length != 8) return null;
        return await db.Ceps.AsNoTracking().FirstOrDefaultAsync(x => x.Code == digits, ct);
    }
}
