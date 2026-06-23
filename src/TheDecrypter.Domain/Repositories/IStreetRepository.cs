using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

/// <summary>Rol de Ruas (Blumenau). Busca flexível: data (tem "/"), número
/// (código ou lei) ou texto (nome).</summary>
public interface IStreetRepository
{
    Task<IReadOnlyList<Street>> SearchAsync(string query, int limit, CancellationToken ct = default);
}
