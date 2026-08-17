using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Ef.Repositories;

public class PosteRepository(DecrypterDbContext db) : IPosteRepository
{
    // A escala e a conversão para metros moram em `PosteGeo` (domínio), com a
    // explicação de por que precisam bater com a coluna gerada do schema.
    private const double EscalaLng = PosteGeo.EscalaLng;
    private const double MetrosPorGrau = PosteGeo.MetrosPorGrau;

    public Task<Poste?> ByPlaquetaAsync(string plaqueta, CancellationToken ct = default) =>
        db.Postes.FirstOrDefaultAsync(p => p.Plaqueta == plaqueta, ct);

    public async Task<IReadOnlyList<Poste>> SearchAsync(
        string q, int limit, CancellationToken ct = default)
    {
        var termo = q.Trim();
        if (termo.Length < 2) return [];
        var n = Math.Clamp(limit, 1, 200);

        // Só dígitos → é plaqueta: prefixo, e a exata primeiro.
        if (termo.All(char.IsDigit))
        {
            var prefixo = $"{termo}%";
            return await db.Postes
                .FromSql(
                    $@"SELECT * FROM poste WHERE plaqueta LIKE {prefixo}
                       ORDER BY length(plaqueta), plaqueta LIMIT {n}")
                .ToListAsync(ct);
        }

        var padrao = $"%{termo}%";
        return await db.Postes
            .FromSql(
                $@"SELECT * FROM poste
                   WHERE immutable_unaccent(rua) ILIKE immutable_unaccent({padrao})
                      OR immutable_unaccent(bairro) ILIKE immutable_unaccent({padrao})
                   ORDER BY rua, numero NULLS LAST LIMIT {n}")
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Poste>> NearAsync(
        double lat, double lng, int limit, CancellationToken ct = default)
    {
        var lngEscalado = lng * EscalaLng;
        var n = Math.Clamp(limit, 1, 200);
        var hits = await db.Postes
            .FromSql(
                $@"SELECT * FROM poste
                   ORDER BY coord_bnu <-> point({lat}, {lngEscalado})
                   LIMIT {n}")
            .ToListAsync(ct);

        // A ORDENAÇÃO fica no banco, que é onde o índice GiST vive; o número em
        // metros é aritmética trivial e sai daqui. Trazê-lo como alias do SELECT
        // não funciona: `DistanciaMetros` é `Ignore` no modelo (não é coluna), e
        // o EF simplesmente não preenche — foi assim que ele voltou nulo.
        foreach (var p in hits)
        {
            var dLat = p.Lat - lat;
            var dLng = p.Lng * EscalaLng - lngEscalado;
            p.DistanciaMetros = Math.Sqrt(dLat * dLat + dLng * dLng) * MetrosPorGrau;
        }
        return hits;
    }

    public async Task<(IReadOnlyList<Poste>, bool)> BboxAsync(
        double sul, double norte, double oeste, double leste, int limit, CancellationToken ct = default)
    {
        // O mesmo índice GiST atende a caixa, desde que ela seja construída na
        // MESMA escala da coluna gerada.
        var n = Math.Clamp(limit, 1, 2000);
        var oesteEsc = oeste * EscalaLng;
        var lesteEsc = leste * EscalaLng;
        var hits = await db.Postes
            .FromSql(
                $@"SELECT * FROM poste
                   WHERE coord_bnu <@ box(point({sul}, {oesteEsc}), point({norte}, {lesteEsc}))
                   LIMIT {n + 1}")
            .ToListAsync(ct);

        // Pedimos um a mais só para saber se cortou — no zoom da cidade a caixa
        // contém as 45 mil linhas, e devolver tudo faria desta a maior resposta
        // da API por uma ordem de grandeza.
        var truncado = hits.Count > n;
        return (truncado ? hits.Take(n).ToList() : hits, truncado);
    }

    public Task<int> CountAsync(CancellationToken ct = default) => db.Postes.CountAsync(ct);
}
