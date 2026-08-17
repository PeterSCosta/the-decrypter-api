using TheDecrypter.Domain.Entities;

namespace TheDecrypter.Domain.Repositories;

/// <summary>Acesso à base de CEP (Postgres) — incluindo busca por curinga.</summary>
public interface ICepRepository
{
    /// <summary>
    /// Busca CEPs por padrão com curinga: dígitos literais + `x X * _ ?`.
    /// Ex.: "88xxx500" casa qualquer CEP iniciando 88, terminando 500.
    /// </summary>
    /// <summary>
    /// Busca com curinga. Devolve também o total REAL de acertos, não o número
    /// de linhas trazidas: o rótulo do app mostra "88xxx500 · 213 CEP(s)", e com
    /// a contagem limitada ele passaria a mentir assim que houvesse mais acertos
    /// que o teto.
    /// </summary>
    Task<(IReadOnlyList<Cep> Hits, int Total)> SearchWildcardAsync(
        string pattern, int limit, CancellationToken ct = default);

    /// <summary>CEP exato (8 dígitos) na base local, ou null.</summary>
    Task<Cep?> GetByCodeAsync(string code, CancellationToken ct = default);
}
