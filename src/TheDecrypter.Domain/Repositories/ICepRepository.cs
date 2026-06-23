using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

/// <summary>Acesso à base de CEP (Postgres) — incluindo busca por curinga.</summary>
public interface ICepRepository
{
    /// <summary>
    /// Busca CEPs por padrão com curinga: dígitos literais + `x X * _ ?`.
    /// Ex.: "88xxx500" casa qualquer CEP iniciando 88, terminando 500.
    /// </summary>
    Task<IReadOnlyList<Cep>> SearchWildcardAsync(string pattern, int limit, CancellationToken ct = default);

    /// <summary>CEP exato (8 dígitos) na base local, ou null.</summary>
    Task<Cep?> GetByCodeAsync(string code, CancellationToken ct = default);
}
