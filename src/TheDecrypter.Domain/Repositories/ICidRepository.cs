using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

public interface ICidRepository
{
    /// <summary>Código exato, nas duas grafias (<c>A00.0</c> ou <c>A000</c>).</summary>
    Task<Cid?> ByCodigoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Doença por nome, sem acento e no meio da frase.</summary>
    Task<IReadOnlyList<Cid>> SearchAsync(string q, int limit, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);
}
