using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Ef.Repositories;

public class CidRepository(DecrypterDbContext db) : ICidRepository
{
    public Task<Cid?> ByCodigoAsync(string codigo, CancellationToken ct = default)
    {
        // Normaliza aqui também, e não só no portão: quem chama o repositório de
        // outro caminho (a Biblioteca, um teste) não deveria precisar saber que
        // a coluna guarda a grafia sem ponto.
        var c = CidCodigo.Normalizar(codigo);
        return c is null
            ? Task.FromResult<Cid?>(null)
            : db.Cids.FirstOrDefaultAsync(x => x.Codigo == c, ct);
    }

    public async Task<IReadOnlyList<Cid>> SearchAsync(
        string q, int limit, CancellationToken ct = default)
    {
        var termo = q.Trim();
        // Abaixo de 4 letras não é busca: "ano" casa dentro de "melanoma" e
        // devolveria doença para quem digitou qualquer coisa.
        if (termo.Length < 4) return [];
        var n = Math.Clamp(limit, 1, 200);

        // O padrão (início de palavra, metacaracteres neutralizados) mora no
        // domínio, onde o teste alcança. `immutable_unaccent` dos dois lados é o
        // que faz "diarreia" achar "Diarréia": a base de 2008 é acentuada à moda
        // antiga, e ninguém digita o acento que o acordo ortográfico tirou.
        var padrao = CidCodigo.PadraoDeNome(termo);
        return await db.Cids
            .FromSql(
                $@"SELECT * FROM cid
                   WHERE immutable_unaccent(descricao) ~* immutable_unaccent({padrao})
                   ORDER BY length(descricao), codigo LIMIT {n}")
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(CancellationToken ct = default) => db.Cids.CountAsync(ct);
}
