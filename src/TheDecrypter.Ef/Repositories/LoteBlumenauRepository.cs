using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Ef.Repositories;

public class LoteBlumenauRepository(DecrypterDbContext db) : ILoteBlumenauRepository
{
    public Task<LoteBlumenau?> ByInscricaoAsync(string entrada, CancellationToken ct = default)
    {
        var f = InscricaoBlumenau.Normalizar(entrada);
        if (f is null) return Task.FromResult<LoteBlumenau?>(null);

        // Busca pela PK quando dá, e só cai no IQ quando a forma digitada não
        // permite reconstruir os 15 dígitos (falta a unidade).
        return f.Inscricao is { } insc
            ? db.LotesBlumenau.FirstOrDefaultAsync(l => l.Inscricao == insc, ct)
            : db.LotesBlumenau.FirstOrDefaultAsync(l => l.Iq == f.Iq, ct);
    }

    public Task<int> CountAsync(CancellationToken ct = default) => db.LotesBlumenau.CountAsync(ct);
}
