using Microsoft.EntityFrameworkCore;
using TheDecrypter.Domain.Entities;
using TheDecrypter.Domain.Repositories;
using TheDecrypter.Domain.Search;

namespace TheDecrypter.Ef.Repositories;

public class LoteBlumenauRepository(DecrypterDbContext db) : ILoteBlumenauRepository
{
    /// <summary>
    /// Teto de linhas lidas antes de colapsar por IQ. Um mesmo terreno aparece
    /// uma vez por unidade (apartamento, box) e o campeão medido tem 13 — 64
    /// dá folga de sobra sem abrir a porta para uma leitura grande.
    /// </summary>
    private const int TetoLinhas = 64;

    public async Task<IReadOnlyList<LoteBlumenau>> BuscarAsync(
        string entrada, int limite, CancellationToken ct = default)
    {
        var f = InscricaoBlumenau.Normalizar(entrada);
        if (f is null) return [];

        // Os 15 dígitos são a PK: acerto exato, sem candidato nenhum.
        if (f.Inscricao is { } insc)
        {
            var um = await db.LotesBlumenau.FirstOrDefaultAsync(l => l.Inscricao == insc, ct);
            return um is null ? [] : [um];
        }

        // Um `IN` sobre `ix_lote_blumenau_iq` com meia dúzia de valores — é o
        // mesmo índice que a grafia com hífen já usava, sem coluna nova.
        var cand = f.Candidatos;
        var linhas = await db.LotesBlumenau
            .Where(l => cand.Contains(l.Iq!))
            // Menor inscrição primeiro = unidade `000`, o TERRENO. Quem digitou
            // o número do lote quer o lote, não o apartamento 12 dele.
            .OrderBy(l => l.Inscricao)
            .Take(TetoLinhas)
            .ToListAsync(ct);

        // Uma linha por IQ: treze unidades do mesmo terreno não são treze
        // respostas, são a mesma resposta treze vezes.
        return [.. linhas.GroupBy(l => l.Iq).Select(g => g.First()).Take(limite)];
    }

    public Task<int> CountAsync(CancellationToken ct = default) => db.LotesBlumenau.CountAsync(ct);
}
