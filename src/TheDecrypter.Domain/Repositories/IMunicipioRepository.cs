using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

/// <summary>Municípios do IBGE: por código (7 ou 6 dígitos) ou por nome.</summary>
public interface IMunicipioRepository
{
    Task<Municipio?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Municipio>> SearchByNameAsync(string name, int limit, CancellationToken ct = default);
}
